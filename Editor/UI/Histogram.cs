#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrWario.Editor.UI
{
    /// <summary>
    /// Histogram that auto-buckets raw data values into bins and displays frequency counts.
    /// </summary>
    public class Histogram : ChartElement
    {
        private float[] _rawValues;
        private int _bucketCount = 20;

        private int[] _buckets;
        private float _dataMin;
        private float _dataMax;
        private float _bucketWidth;
        private int _maxCount;

        /// <summary>
        /// Set raw data values to be bucketed into a histogram.
        /// </summary>
        public void SetData(float[] rawValues, int bucketCount = 20)
        {
            _rawValues = rawValues;
            _bucketCount = Mathf.Max(1, bucketCount);

            bool hasData = rawValues != null && rawValues.Length > 0;
            SetHasData(hasData);

            if (hasData)
                ComputeBuckets();
            else
                _buckets = null;

            MarkDirtyRepaint();
        }

        private void ComputeBuckets()
        {
            // Find min/max
            _dataMin = float.MaxValue;
            _dataMax = float.MinValue;

            for (int i = 0; i < _rawValues.Length; i++)
            {
                if (_rawValues[i] < _dataMin) _dataMin = _rawValues[i];
                if (_rawValues[i] > _dataMax) _dataMax = _rawValues[i];
            }

            // Handle edge case where all values are identical
            if (_dataMax <= _dataMin)
            {
                _dataMax = _dataMin + 1f;
            }

            _bucketWidth = (_dataMax - _dataMin) / _bucketCount;
            _buckets = new int[_bucketCount];
            _maxCount = 0;

            for (int i = 0; i < _rawValues.Length; i++)
            {
                int bucketIndex = Mathf.FloorToInt((_rawValues[i] - _dataMin) / _bucketWidth);
                // Clamp the last value that equals _dataMax into the final bucket
                if (bucketIndex >= _bucketCount) bucketIndex = _bucketCount - 1;

                _buckets[bucketIndex]++;
                if (_buckets[bucketIndex] > _maxCount)
                    _maxCount = _buckets[bucketIndex];
            }

            if (_maxCount <= 0) _maxCount = 1;
        }

        protected override void DrawChart(Painter2D painter)
        {
            if (_buckets == null || _buckets.Length == 0) return;

            int count = _buckets.Length;
            float barGap = 1f;
            float totalGaps = (count - 1) * barGap;
            float barWidth = (ChartWidth - totalGaps) / count;
            if (barWidth < 1f) barWidth = 1f;

            float bottomY = resolvedStyle.height - Padding;
            float yMaxScaled = _maxCount * 1.1f;

            Color barColor = new Color(0.4f, 0.7f, 0.4f, 1f);

            for (int i = 0; i < count; i++)
            {
                float x = Padding + i * (barWidth + barGap);
                float topY = MapY(_buckets[i], 0f, yMaxScaled);
                float height = bottomY - topY;

                if (height <= 0f) continue;

                painter.fillColor = barColor;
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, topY));
                painter.LineTo(new Vector2(x + barWidth, topY));
                painter.LineTo(new Vector2(x + barWidth, bottomY));
                painter.LineTo(new Vector2(x, bottomY));
                painter.ClosePath();
                painter.Fill();
            }
        }

        protected override void OnHover(Vector2 localPos)
        {
            if (_buckets == null || _buckets.Length == 0)
            {
                HideTooltip();
                return;
            }

            int count = _buckets.Length;
            float barGap = 1f;
            float totalGaps = (count - 1) * barGap;
            float barWidth = (ChartWidth - totalGaps) / count;
            if (barWidth < 1f) barWidth = 1f;

            float relX = localPos.x - Padding;
            int index = Mathf.FloorToInt(relX / (barWidth + barGap));

            if (index >= 0 && index < count)
            {
                float rangeStart = _dataMin + index * _bucketWidth;
                float rangeEnd = rangeStart + _bucketWidth;
                ShowTooltip(localPos.x, localPos.y,
                    $"[{rangeStart:F1} - {rangeEnd:F1}]: {_buckets[index]} samples");
            }
            else
            {
                HideTooltip();
            }
        }
    }
}
#endif
