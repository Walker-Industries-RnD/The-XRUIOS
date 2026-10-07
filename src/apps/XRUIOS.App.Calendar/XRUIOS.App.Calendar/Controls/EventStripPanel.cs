using System;
using System.Windows;
using System.Windows.Controls;

namespace XRUIOS.App.Calendar.Controls
{
    /// <summary>
    /// Layout panel for the Day view's event row. Fills the whole width with
    /// equal-width EventCards, no gaps — matches the flush strip in the mockup.
    /// A UniformGrid would work but leaves rounding pixels at the right edge;
    /// this variant distributes remainder pixels across the leading cards.
    /// </summary>
    public sealed class EventStripPanel : Panel
    {
        protected override Size MeasureOverride(Size available)
        {
            var count = Math.Max(1, Children.Count);
            var childBox = new Size(available.Width / count, available.Height);
            foreach (UIElement child in Children) child.Measure(childBox);
            return available;
        }

        protected override Size ArrangeOverride(Size final)
        {
            var count = Math.Max(1, Children.Count);
            var basew = (int)(final.Width / count);
            var extra = (int)final.Width - basew * count; // pixels to distribute
            double x = 0;
            for (var i = 0; i < Children.Count; i++)
            {
                var w = basew + (i < extra ? 1 : 0);
                Children[i].Arrange(new Rect(x, 0, w, final.Height));
                x += w;
            }
            return final;
        }
    }
}
