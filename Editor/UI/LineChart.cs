#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrWario.Editor.UI
{
    /// <summary>
    /// Line chart supporting single or multiple data series with optional horizontal reference lines.
    /// </summary>
    public class LineChart : ChartElement
    {
        private struct Series
        {
            public float[] Values;
            public float[] TimeOffsets;
            public string Label;
            public Color Color;
        }

        private struct ReferenceLine
        {
            public float Value;
            public Color Color;
            public string Label;
        }

        private readonly List<Series> _series = new List<Series>();
        private readonly List<ReferenceLine> _referenceLines = new List<ReferenceLine>();

        private float _yMin;
        private float _yMax;
        private float _xMin;
        private float _xMax;

        /// <summary>
        /// Set a single data series, replacing any existing series.
        /// </summary>
        public void SetData(float[] values, float[] timeOffsets, string label, Color color)
        {
            _series.Clear();
            AddSeries(values, timeOffsets, label, color);
        }

        /// <summary>
        /// Add an additional data series to the chart.
        /// </summary>
        public void AddSeries(float[] values, float[] timeOffsets, string label, Color color)
        {
            if (values == null || timeOffsets == null || values.Length == 0 || timeOffsets.Length == 0)
            {
                SetHasData(_series.Count > 0);
                MarkDirtyRepaint();
                return;
            }

            _series.Add(new Series
            {
                Values = values,
                TimeOffsets = timeOffsets,
                Label = label,
                Color = color
            });

            RecalculateBounds();
            SetHasData(true);
            MarkDirtyRepaint();
        }

        /// <summary>
        /// Set horizontal reference lines (e.g., 60fps target at 16.67ms).
        /// </summary>
        public void SetReferenceLines(params (float value, Color color, string label)[] lines)
        {
            _referenceLines.Clear();
            if (lines != null)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    _referenceLines.Add(new ReferenceLine
                    {
                        Value = lines[i].value,
                        Color = lines[i].color,
                        Label = lines[i].label
                    });
                }
            }

            RecalculateBounds();
            MarkDirtyRepaint();
        }

        private void RecalculateBounds()
        {
            _yMin = 0f;
            _yMax = 0f;
            _xMin = float.MaxValue;
            _xMax = float.MinValue;

            for (int s = 0; s < _series.Count; s++)
            {
                var series = _series[s];
                for (int i = 0; i < series.Values.Length; i++)
                {
                    if (series.Values[i] > _yMax) _yMax = series.Values[i];
                }

                for (int i = 0; i < series.TimeOffsets.Length; i++)
                {
                    if (series.TimeOffsets[i] < _xMin) _xMin = series.TimeOffsets[i];
                    if (series.TimeOffsets[i] > _xMax) _xMax = series.TimeOffsets[i];
                }
            }

            // Include reference lines in Y bounds
            for (int i = 0; i < _referenceLines.Count; i++)
            {
                if (_referenceLines[i].Value > _yMax) _yMax = _referenceLines[i].Value;
            }

            _yMax *= 1.1f;

            if (_xMin == float.MaxValue) _xMin = 0f;
            if (_xMax == float.MinValue) _xMax = 1f;
            if (_yMax <= 0f) _yMax = 1f;
        }

        protected override void DrawChart(Painter2D painter)
        {
            // Draw reference lines first (behind data)
            for (int i = 0; i < _referenceLines.Count; i++)
            {
                DrawReferenceLine(painter, _referenceLines[i].Value, _yMin, _yMax, _referenceLines[i].Color);
            }

            // Draw each series
            for (int s = 0; s < _series.Count; s++)
            {
                var series = _series[s];
                int count = Mathf.Min(series.Values.Length, series.TimeOffsets.Length);
                if (count < 2) continue;

                painter.strokeColor = series.Color;
                painter.lineWidth = 2f;
                painter.BeginPath();

                float px = MapX(series.TimeOffsets[0], _xMin, _xMax);
                float py = MapY(series.Values[0], _yMin, _yMax);
                painter.MoveTo(new Vector2(px, py));

                for (int i = 1; i < count; i++)
                {
                    px = MapX(series.TimeOffsets[i], _xMin, _xMax);
                    py = MapY(series.Values[i], _yMin, _yMax);
                    painter.LineTo(new Vector2(px, py));
                }

                painter.Stroke();
            }
        }

        protected override void OnHover(Vector2 localPos)
        {
            if (_series.Count == 0)
            {
                HideTooltip();
                return;
            }

            // Find the nearest data point across all series based on mouse X
            float mouseDataX = _xMin + (localPos.x - Padding) / ChartWidth * (_xMax - _xMin);

            string bestLabel = null;
            float bestValue = 0f;
            float bestTime = 0f;
            float bestDist = float.MaxValue;

            for (int s = 0; s < _series.Count; s++)
            {
                var series = _series[s];
                int count = Mathf.Min(series.Values.Length, series.TimeOffsets.Length);

                for (int i = 0; i < count; i++)
                {
                    float dist = Mathf.Abs(series.TimeOffsets[i] - mouseDataX);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestLabel = series.Label;
                        bestValue = series.Values[i];
                        bestTime = series.TimeOffsets[i];
                    }
                }
            }

            if (bestLabel != null)
            {
                ShowTooltip(localPos.x, localPos.y, $"{bestLabel}: {bestValue:F2} at {bestTime:F2}s");
            }
            else
            {
                HideTooltip();
            }
        }
    }
}
#endif
