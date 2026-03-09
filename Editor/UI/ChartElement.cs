#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace DrWario.Editor.UI
{
    /// <summary>
    /// Base VisualElement for chart rendering with Painter2D.
    /// Provides coordinate mapping, tooltip infrastructure, and empty state handling.
    /// </summary>
    public abstract class ChartElement : VisualElement
    {
        protected const float Padding = 4f;

        private readonly Label _tooltip;
        private readonly Label _emptyLabel;
        private bool _hasData;

        protected float ChartWidth => resolvedStyle.width - Padding * 2;
        protected float ChartHeight => resolvedStyle.height - Padding * 2;

        protected ChartElement()
        {
            style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            style.borderBottomLeftRadius = 4;
            style.borderBottomRightRadius = 4;
            style.borderTopLeftRadius = 4;
            style.borderTopRightRadius = 4;

            generateVisualContent += OnGenerateVisualContent;

            // Tooltip label (hidden by default)
            _tooltip = new Label
            {
                style =
                {
                    position = Position.Absolute,
                    backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f),
                    color = Color.white,
                    fontSize = 11,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 3,
                    paddingBottom = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    display = DisplayStyle.None
                }
            };
            Add(_tooltip);

            // Empty state label
            _emptyLabel = new Label("No data")
            {
                style =
                {
                    position = Position.Absolute,
                    color = new Color(0.5f, 0.5f, 0.5f, 1f),
                    fontSize = 12,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    width = Length.Percent(100),
                    height = Length.Percent(100)
                }
            };
            Add(_emptyLabel);

            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
        }

        protected void SetHasData(bool hasData)
        {
            _hasData = hasData;
            _emptyLabel.style.display = hasData ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>
        /// Map a data value to pixel X coordinate within the chart area.
        /// </summary>
        protected float MapX(float value, float minValue, float maxValue)
        {
            if (maxValue <= minValue) return Padding;
            return Padding + (value - minValue) / (maxValue - minValue) * ChartWidth;
        }

        /// <summary>
        /// Map a data value to pixel Y coordinate within the chart area (Y increases downward).
        /// </summary>
        protected float MapY(float value, float minValue, float maxValue)
        {
            if (maxValue <= minValue) return resolvedStyle.height - Padding;
            float normalized = (value - minValue) / (maxValue - minValue);
            return resolvedStyle.height - Padding - normalized * ChartHeight;
        }

        protected void ShowTooltip(float x, float y, string text)
        {
            _tooltip.text = text;
            _tooltip.style.display = DisplayStyle.Flex;
            _tooltip.style.left = Mathf.Max(0, x - 40);
            _tooltip.style.top = Mathf.Max(0, y - 30);
        }

        protected void HideTooltip()
        {
            _tooltip.style.display = DisplayStyle.None;
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (!_hasData || ChartWidth <= 0 || ChartHeight <= 0) return;
            var painter = ctx.painter2D;
            DrawChart(painter);
        }

        /// <summary>
        /// Override to draw chart content using Painter2D.
        /// </summary>
        protected abstract void DrawChart(Painter2D painter);

        /// <summary>
        /// Override to handle mouse hover and show tooltips.
        /// localPos is the mouse position relative to this element.
        /// </summary>
        protected virtual void OnHover(Vector2 localPos) { }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!_hasData) return;
            OnHover(evt.localMousePosition);
        }

        private void OnMouseLeave(MouseLeaveEvent evt)
        {
            HideTooltip();
        }

        /// <summary>
        /// Draw a horizontal reference line at the given Y value.
        /// </summary>
        protected void DrawReferenceLine(Painter2D painter, float yValue, float minY, float maxY, Color color)
        {
            float py = MapY(yValue, minY, maxY);
            painter.strokeColor = color;
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(Padding, py));
            painter.LineTo(new Vector2(resolvedStyle.width - Padding, py));
            painter.Stroke();
        }
    }
}
#endif
