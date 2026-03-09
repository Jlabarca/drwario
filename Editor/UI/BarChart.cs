#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrWario.Editor.UI
{
    /// <summary>
    /// Bar chart supporting simple bars and grouped (side-by-side) bars with threshold highlighting.
    /// </summary>
    public class BarChart : ChartElement
    {
        private const float BarGap = 2f;

        private float[] _values;
        private string[] _labels;
        private Color _color = new Color(0.3f, 0.6f, 1f, 1f);

        private float[][] _groups;
        private string[] _groupLabels;
        private Color[] _groupColors;

        private bool _isGrouped;
        private float _yMax;

        /// <summary>
        /// Bars exceeding this threshold are drawn in red.
        /// Set to 0 or negative to disable threshold highlighting.
        /// </summary>
        public float HighlightThreshold { get; set; }

        /// <summary>
        /// Set simple bar data, replacing any grouped data.
        /// </summary>
        public void SetData(float[] values, string[] labels, Color color)
        {
            _isGrouped = false;
            _groups = null;
            _groupLabels = null;
            _groupColors = null;

            _values = values;
            _labels = labels;
            _color = color;

            bool hasData = values != null && values.Length > 0;
            SetHasData(hasData);

            if (hasData)
            {
                _yMax = 0f;
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i] > _yMax) _yMax = values[i];
                }
                _yMax *= 1.1f;
                if (_yMax <= 0f) _yMax = 1f;
            }

            MarkDirtyRepaint();
        }

        /// <summary>
        /// Set grouped bar data for side-by-side comparison (e.g., CPU vs GPU).
        /// groups[seriesIndex][barIndex] — all inner arrays should be the same length.
        /// </summary>
        public void SetGroupedData(float[][] groups, string[] groupLabels, Color[] colors)
        {
            _isGrouped = true;
            _values = null;
            _labels = null;

            _groups = groups;
            _groupLabels = groupLabels;
            _groupColors = colors;

            bool hasData = groups != null && groups.Length > 0 && groups[0] != null && groups[0].Length > 0;
            SetHasData(hasData);

            if (hasData)
            {
                _yMax = 0f;
                for (int s = 0; s < groups.Length; s++)
                {
                    if (groups[s] == null) continue;
                    for (int i = 0; i < groups[s].Length; i++)
                    {
                        if (groups[s][i] > _yMax) _yMax = groups[s][i];
                    }
                }
                _yMax *= 1.1f;
                if (_yMax <= 0f) _yMax = 1f;
            }

            MarkDirtyRepaint();
        }

        protected override void DrawChart(Painter2D painter)
        {
            if (_isGrouped)
                DrawGroupedBars(painter);
            else
                DrawSimpleBars(painter);
        }

        private void DrawSimpleBars(Painter2D painter)
        {
            if (_values == null || _values.Length == 0) return;

            int count = _values.Length;
            float totalGaps = (count - 1) * BarGap;
            float barWidth = (ChartWidth - totalGaps) / count;
            if (barWidth < 1f) barWidth = 1f;

            float bottomY = resolvedStyle.height - Padding;

            for (int i = 0; i < count; i++)
            {
                float x = Padding + i * (barWidth + BarGap);
                float topY = MapY(_values[i], 0f, _yMax);

                Color barColor = (HighlightThreshold > 0f && _values[i] > HighlightThreshold)
                    ? new Color(0.9f, 0.2f, 0.2f, 1f)
                    : _color;

                DrawBar(painter, x, topY, barWidth, bottomY - topY, barColor);
            }
        }

        private void DrawGroupedBars(Painter2D painter)
        {
            if (_groups == null || _groups.Length == 0) return;

            int seriesCount = _groups.Length;
            int barCount = _groups[0].Length;
            float totalGaps = (barCount - 1) * BarGap;
            float groupWidth = (ChartWidth - totalGaps) / barCount;
            if (groupWidth < 1f) groupWidth = 1f;

            float subBarWidth = groupWidth / seriesCount;
            float bottomY = resolvedStyle.height - Padding;

            for (int g = 0; g < barCount; g++)
            {
                float groupX = Padding + g * (groupWidth + BarGap);

                for (int s = 0; s < seriesCount; s++)
                {
                    if (_groups[s] == null || g >= _groups[s].Length) continue;

                    float value = _groups[s][g];
                    float x = groupX + s * subBarWidth;
                    float topY = MapY(value, 0f, _yMax);

                    Color barColor = (HighlightThreshold > 0f && value > HighlightThreshold)
                        ? new Color(0.9f, 0.2f, 0.2f, 1f)
                        : (_groupColors != null && s < _groupColors.Length ? _groupColors[s] : _color);

                    DrawBar(painter, x, topY, subBarWidth, bottomY - topY, barColor);
                }
            }
        }

        private void DrawBar(Painter2D painter, float x, float y, float width, float height, Color color)
        {
            if (height <= 0f) return;

            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, y));
            painter.LineTo(new Vector2(x + width, y));
            painter.LineTo(new Vector2(x + width, y + height));
            painter.LineTo(new Vector2(x, y + height));
            painter.ClosePath();
            painter.Fill();
        }

        protected override void OnHover(Vector2 localPos)
        {
            if (_isGrouped)
                OnHoverGrouped(localPos);
            else
                OnHoverSimple(localPos);
        }

        private void OnHoverSimple(Vector2 localPos)
        {
            if (_values == null || _values.Length == 0)
            {
                HideTooltip();
                return;
            }

            int count = _values.Length;
            float totalGaps = (count - 1) * BarGap;
            float barWidth = (ChartWidth - totalGaps) / count;
            if (barWidth < 1f) barWidth = 1f;

            float relX = localPos.x - Padding;
            int index = Mathf.FloorToInt(relX / (barWidth + BarGap));

            if (index >= 0 && index < count)
            {
                string label = (_labels != null && index < _labels.Length) ? _labels[index] : $"Bar {index}";
                ShowTooltip(localPos.x, localPos.y, $"{label}: {_values[index]:F2}");
            }
            else
            {
                HideTooltip();
            }
        }

        private void OnHoverGrouped(Vector2 localPos)
        {
            if (_groups == null || _groups.Length == 0)
            {
                HideTooltip();
                return;
            }

            int seriesCount = _groups.Length;
            int barCount = _groups[0].Length;
            float totalGaps = (barCount - 1) * BarGap;
            float groupWidth = (ChartWidth - totalGaps) / barCount;
            if (groupWidth < 1f) groupWidth = 1f;

            float relX = localPos.x - Padding;
            int groupIndex = Mathf.FloorToInt(relX / (groupWidth + BarGap));

            if (groupIndex >= 0 && groupIndex < barCount)
            {
                float withinGroup = relX - groupIndex * (groupWidth + BarGap);
                float subBarWidth = groupWidth / seriesCount;
                int seriesIndex = Mathf.FloorToInt(withinGroup / subBarWidth);
                seriesIndex = Mathf.Clamp(seriesIndex, 0, seriesCount - 1);

                string groupLabel = (_groupLabels != null && groupIndex < _groupLabels.Length)
                    ? _groupLabels[groupIndex]
                    : $"Group {groupIndex}";

                float value = (seriesIndex < _groups.Length && _groups[seriesIndex] != null && groupIndex < _groups[seriesIndex].Length)
                    ? _groups[seriesIndex][groupIndex]
                    : 0f;

                ShowTooltip(localPos.x, localPos.y, $"{groupLabel}: {value:F2}");
            }
            else
            {
                HideTooltip();
            }
        }
    }
}
#endif
