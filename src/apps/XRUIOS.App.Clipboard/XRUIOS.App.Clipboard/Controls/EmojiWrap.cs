using System.Windows;
using System.Windows.Controls;

namespace XRUIOS.App.Clipboard.Controls
{
    /// <summary>
    /// Fixed-cell reflowing panel just for the emoji grid. Stays a Panel
    /// subclass (measure/arrange override) - a UserControl cannot replace a
    /// Panel because Panel's layout hooks are what let this reflow at all.
    /// </summary>
    public sealed class EmojiWrap : Panel
    {
        private const double CellSize = 44;
        private const double Gap = 6;

        protected override Size MeasureOverride(Size available)
        {
            var box = new Size(CellSize, CellSize);
            foreach (UIElement c in Children) c.Measure(box);
            var perRow = System.Math.Max(1, (int)((available.Width + Gap) / (CellSize + Gap)));
            var rows = (int)System.Math.Ceiling((double)Children.Count / perRow);
            return new Size(available.Width, rows * (CellSize + Gap));
        }

        protected override Size ArrangeOverride(Size final)
        {
            var perRow = System.Math.Max(1, (int)((final.Width + Gap) / (CellSize + Gap)));
            for (var i = 0; i < Children.Count; i++)
            {
                var col = i % perRow;
                var row = i / perRow;
                Children[i].Arrange(new Rect(col * (CellSize + Gap), row * (CellSize + Gap), CellSize, CellSize));
            }
            return final;
        }
    }
}
