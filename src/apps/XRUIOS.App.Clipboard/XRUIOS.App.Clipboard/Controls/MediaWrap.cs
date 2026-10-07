using System;
using System.Windows;
using System.Windows.Controls;

namespace XRUIOS.App.Clipboard.Controls
{
    /// <summary>
    /// Masonry-ish layout for the right-hand media gallery. Each child
    /// (typically a MediaTile) declares its own width+height; this panel
    /// packs them into the shortest available column so the pattern stays
    /// visually balanced like the mockup's mixed-size thumbnail grid.
    /// Stays a Panel subclass because measure/arrange overrides are what
    /// make the column-packing work.
    /// </summary>
    public sealed class MediaWrap : Panel
    {
        private const double Gap = 6;
        private const int Columns = 3;

        protected override Size MeasureOverride(Size available)
        {
            foreach (UIElement c in Children) c.Measure(new Size(available.Width, double.PositiveInfinity));
            var heights = new double[Columns];
            for (var i = 0; i < Children.Count; i++)
            {
                var idx = ShortestIndex(heights);
                heights[idx] += Children[i].DesiredSize.Height + Gap;
            }
            var totalHeight = 0.0;
            foreach (var h in heights) totalHeight = Math.Max(totalHeight, h);
            return new Size(available.Width, totalHeight);
        }

        protected override Size ArrangeOverride(Size final)
        {
            var colW = (final.Width - Gap * (Columns - 1)) / Columns;
            var y = new double[Columns];
            for (var i = 0; i < Children.Count; i++)
            {
                var idx = ShortestIndex(y);
                var child = Children[i];
                var h = child.DesiredSize.Height;
                var x = idx * (colW + Gap);
                child.Arrange(new Rect(x, y[idx], colW, h));
                y[idx] += h + Gap;
            }
            return final;
        }

        private static int ShortestIndex(double[] heights)
        {
            var min = 0;
            for (var i = 1; i < heights.Length; i++)
                if (heights[i] < heights[min]) min = i;
            return min;
        }
    }
}
