#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrWario.Editor.UI
{
    /// <summary>
    /// Interactive timeline visualization with zoom/pan, lane-based event rendering,
    /// and tooltip on hover. Extends ChartElement for Painter2D drawing infrastructure.
    /// </summary>
    public class TimelineElement : ChartElement
    {
        private List<TimelineEvent> _events = new();
        private float _zoomLevel = 1.0f;
        private float _panOffsetX;
        private bool _isDragging;
        private float _dragStartX;
        private float _dragStartPan;
        private float _minTimestamp;
        private float _maxTimestamp;

        private const float MinZoom = 0.1f;
        private const float MaxZoom = 50f;
        private const float EventHeight = 10f;
        private const float PointRadius = 4f;

        // Lane colors per event type
        private static readonly Dictionary<TimelineEventType, Color> LaneColors = new()
        {
            { TimelineEventType.FrameSpike, new Color(0.267f, 0.533f, 1f) },    // #4488FF
            { TimelineEventType.GCAlloc, new Color(1f, 0.533f, 0.267f) },       // #FF8844
            { TimelineEventType.BootStage, new Color(0.267f, 0.733f, 0.267f) }, // #44BB44
            { TimelineEventType.AssetLoad, new Color(0.667f, 0.267f, 1f) },     // #AA44FF
            { TimelineEventType.NetworkEvent, new Color(0.533f, 0.533f, 0.533f) } // #888888
        };

        private static readonly string[] LaneLabels = { "CPU", "GC", "Boot", "Assets", "Network" };
        private const int LaneCount = 5;

        public TimelineElement()
        {
            style.minHeight = 200;
            style.flexGrow = 1;

            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMoveForDrag);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        public void SetEvents(List<TimelineEvent> events)
        {
            _events = events ?? new List<TimelineEvent>();
            SetHasData(_events.Count > 0);

            if (_events.Count > 0)
            {
                _minTimestamp = float.MaxValue;
                _maxTimestamp = float.MinValue;
                foreach (var e in _events)
                {
                    if (e.Timestamp < _minTimestamp) _minTimestamp = e.Timestamp;
                    float end = e.Timestamp + e.Duration;
                    if (end > _maxTimestamp) _maxTimestamp = end;
                    if (e.Timestamp > _maxTimestamp) _maxTimestamp = e.Timestamp;
                }

                // Add a small margin
                float range = _maxTimestamp - _minTimestamp;
                if (range < 0.001f) range = 1f;
                _minTimestamp -= range * 0.02f;
                _maxTimestamp += range * 0.02f;
            }

            _zoomLevel = 1.0f;
            _panOffsetX = 0f;
            MarkDirtyRepaint();
        }

        // -- Zoom (mouse wheel, centered on cursor) --

        private void OnWheel(WheelEvent evt)
        {
            if (_events.Count == 0) return;

            float mouseX = evt.localMousePosition.x;
            float chartX = mouseX - Padding;
            float viewportFraction = chartX / ChartWidth;

            // Zoom factor
            float zoomDelta = evt.delta.y > 0 ? 0.85f : 1.18f;
            float newZoom = Mathf.Clamp(_zoomLevel * zoomDelta, MinZoom, MaxZoom);

            // Adjust pan so the point under the cursor stays in place
            float totalRange = _maxTimestamp - _minTimestamp;
            float oldVisibleRange = totalRange / _zoomLevel;
            float newVisibleRange = totalRange / newZoom;
            float cursorTime = _panOffsetX + viewportFraction * oldVisibleRange;
            _panOffsetX = cursorTime - viewportFraction * newVisibleRange;

            _zoomLevel = newZoom;
            ClampPan();
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        // -- Drag to pan --

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || _events.Count == 0) return;
            _isDragging = true;
            _dragStartX = evt.localMousePosition.x;
            _dragStartPan = _panOffsetX;
            this.CaptureMouse();
            evt.StopPropagation();
        }

        private void OnMouseMoveForDrag(MouseMoveEvent evt)
        {
            if (!_isDragging) return;

            float dx = evt.localMousePosition.x - _dragStartX;
            float totalRange = _maxTimestamp - _minTimestamp;
            float visibleRange = totalRange / _zoomLevel;
            float timeDelta = -dx / ChartWidth * visibleRange;
            _panOffsetX = _dragStartPan + timeDelta;
            ClampPan();
            MarkDirtyRepaint();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (!_isDragging) return;
            _isDragging = false;
            this.ReleaseMouse();
        }

        private void ClampPan()
        {
            float totalRange = _maxTimestamp - _minTimestamp;
            float visibleRange = totalRange / _zoomLevel;
            float maxPan = totalRange - visibleRange;
            _panOffsetX = Mathf.Clamp(_panOffsetX, 0f, Mathf.Max(0f, maxPan));
        }

        // -- Drawing --

        protected override void DrawChart(Painter2D painter)
        {
            float totalRange = _maxTimestamp - _minTimestamp;
            if (totalRange <= 0) return;

            float visibleRange = totalRange / _zoomLevel;
            float viewStart = _minTimestamp + _panOffsetX;
            float viewEnd = viewStart + visibleRange;

            float laneHeight = ChartHeight / LaneCount;

            // Draw lane backgrounds
            for (int lane = 0; lane < LaneCount; lane++)
            {
                float y = Padding + lane * laneHeight;
                float alpha = (lane % 2 == 0) ? 0.06f : 0.03f;
                var laneColor = new Color(1f, 1f, 1f, alpha);

                painter.fillColor = laneColor;
                painter.BeginPath();
                painter.MoveTo(new Vector2(Padding, y));
                painter.LineTo(new Vector2(Padding + ChartWidth, y));
                painter.LineTo(new Vector2(Padding + ChartWidth, y + laneHeight));
                painter.LineTo(new Vector2(Padding, y + laneHeight));
                painter.ClosePath();
                painter.Fill();
            }

            // Draw lane labels
            // (Painter2D does not support text; labels are handled via overlay or omitted)

            // Draw reference lines at 16.67ms and 33.33ms (in the CPU lane)
            DrawDashedReferenceLine(painter, viewStart, viewEnd, 16.67f, laneHeight);
            DrawDashedReferenceLine(painter, viewStart, viewEnd, 33.33f, laneHeight);

            // Draw events
            foreach (var evt in _events)
            {
                // Viewport culling
                float evtEnd = evt.Timestamp + evt.Duration;
                if (evtEnd < viewStart || evt.Timestamp > viewEnd)
                    continue;

                int lane = EventTypeToLane(evt.EventType);
                float laneY = Padding + lane * laneHeight;
                Color color = LaneColors[evt.EventType];

                float x1 = TimestampToX(evt.Timestamp, viewStart, visibleRange);
                float x2 = TimestampToX(evtEnd, viewStart, visibleRange);

                if (evt.Duration > 0.0001f && (x2 - x1) >= 1f)
                {
                    // Duration event: draw rectangle
                    float rectY = laneY + (laneHeight - EventHeight) * 0.5f;
                    painter.fillColor = new Color(color.r, color.g, color.b, 0.7f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x1, rectY));
                    painter.LineTo(new Vector2(x2, rectY));
                    painter.LineTo(new Vector2(x2, rectY + EventHeight));
                    painter.LineTo(new Vector2(x1, rectY + EventHeight));
                    painter.ClosePath();
                    painter.Fill();

                    // Border
                    painter.strokeColor = color;
                    painter.lineWidth = 1f;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x1, rectY));
                    painter.LineTo(new Vector2(x2, rectY));
                    painter.LineTo(new Vector2(x2, rectY + EventHeight));
                    painter.LineTo(new Vector2(x1, rectY + EventHeight));
                    painter.ClosePath();
                    painter.Stroke();
                }
                else
                {
                    // Point event: draw filled circle
                    float cx = x1;
                    float cy = laneY + laneHeight * 0.5f;
                    painter.fillColor = color;
                    painter.BeginPath();
                    painter.Arc(new Vector2(cx, cy), PointRadius, 0f, 360f);
                    painter.ClosePath();
                    painter.Fill();
                }
            }

            // Draw lane separator lines
            painter.strokeColor = new Color(1f, 1f, 1f, 0.15f);
            painter.lineWidth = 1f;
            for (int lane = 1; lane < LaneCount; lane++)
            {
                float y = Padding + lane * laneHeight;
                painter.BeginPath();
                painter.MoveTo(new Vector2(Padding, y));
                painter.LineTo(new Vector2(Padding + ChartWidth, y));
                painter.Stroke();
            }
        }

        private void DrawDashedReferenceLine(Painter2D painter, float viewStart, float viewEnd, float refMs, float laneHeight)
        {
            // Reference lines are drawn in the CPU (FrameSpike) lane as horizontal guidelines
            // These don't map to timestamp axis; we skip them if not meaningful.
            // Instead, we draw them as annotations — thin dashed lines across the full width in the CPU lane.
            float laneY = Padding; // CPU lane is lane 0
            float y = laneY + laneHeight * 0.5f;

            painter.strokeColor = new Color(1f, 1f, 0.4f, 0.25f);
            painter.lineWidth = 1f;

            // Simulate dashed line
            float dashLength = 6f;
            float gapLength = 4f;
            float x = Padding;
            float endX = Padding + ChartWidth;

            while (x < endX)
            {
                float segEnd = Mathf.Min(x + dashLength, endX);
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, y));
                painter.LineTo(new Vector2(segEnd, y));
                painter.Stroke();
                x = segEnd + gapLength;
            }
        }

        private float TimestampToX(float timestamp, float viewStart, float visibleRange)
        {
            if (visibleRange <= 0) return Padding;
            return Padding + (timestamp - viewStart) / visibleRange * ChartWidth;
        }

        private static int EventTypeToLane(TimelineEventType type)
        {
            return type switch
            {
                TimelineEventType.FrameSpike => 0,
                TimelineEventType.GCAlloc => 1,
                TimelineEventType.BootStage => 2,
                TimelineEventType.AssetLoad => 3,
                TimelineEventType.NetworkEvent => 4,
                _ => 0
            };
        }

        // -- Tooltip on hover --

        protected override void OnHover(Vector2 localPos)
        {
            if (_events.Count == 0)
            {
                HideTooltip();
                return;
            }

            float totalRange = _maxTimestamp - _minTimestamp;
            float visibleRange = totalRange / _zoomLevel;
            float viewStart = _minTimestamp + _panOffsetX;

            float laneHeight = ChartHeight / LaneCount;

            // Determine which lane the cursor is in
            int hoveredLane = Mathf.FloorToInt((localPos.y - Padding) / laneHeight);
            if (hoveredLane < 0 || hoveredLane >= LaneCount)
            {
                HideTooltip();
                return;
            }

            // Find nearest event in that lane
            float cursorTime = viewStart + (localPos.x - Padding) / ChartWidth * visibleRange;
            float bestDist = float.MaxValue;
            TimelineEvent? bestEvent = null;

            foreach (var evt in _events)
            {
                if (EventTypeToLane(evt.EventType) != hoveredLane) continue;

                // Check if cursor is within or near the event
                float evtCenter = evt.Timestamp + evt.Duration * 0.5f;
                float dist = Mathf.Abs(cursorTime - evtCenter);

                // Also check if cursor is inside a duration event
                if (evt.Duration > 0 && cursorTime >= evt.Timestamp && cursorTime <= evt.Timestamp + evt.Duration)
                    dist = 0f;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestEvent = evt;
                }
            }

            // Show tooltip if close enough (within 2% of visible range)
            if (bestEvent.HasValue && bestDist < visibleRange * 0.02f)
            {
                var e = bestEvent.Value;
                string tooltip = $"{LaneLabels[hoveredLane]}: {e.Label}\nFrame: {e.FrameIndex}";
                if (e.Duration > 0)
                    tooltip += $"\nDuration: {e.Duration * 1000f:F1}ms";
                ShowTooltip(localPos.x, localPos.y, tooltip);
            }
            else
            {
                HideTooltip();
            }
        }
    }
}
#endif
