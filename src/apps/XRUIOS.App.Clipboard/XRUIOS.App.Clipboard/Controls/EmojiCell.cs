using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XRUIOS.App.Clipboard.Controls
{
    /// <summary>
    /// One dark rounded cell holding an emoji glyph. Stays a Button subclass
    /// (not a UserControl) because click handling belongs to Button; a
    /// UserControl would just wrap one, adding noise.
    /// </summary>
    public sealed class EmojiCell : Button
    {
        public string Glyph { get; }

        public EmojiCell(string glyph)
        {
            Glyph = glyph;
            Background = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14));
            Foreground = new SolidColorBrush(Colors.White);
            BorderThickness = new Thickness(0);
            Padding = new Thickness(0);
            Content = new TextBlock
            {
                Text = glyph,
                FontSize = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
    }
}
