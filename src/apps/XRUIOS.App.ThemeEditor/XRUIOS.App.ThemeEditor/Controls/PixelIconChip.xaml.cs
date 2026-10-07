using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XRUIOS.App.ThemeEditor.Controls
{
    public partial class PixelIconChip : UserControl
    {
        public PixelIconChip() => InitializeComponent();

        public string Glyph
        {
            get => (string)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }
        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(PixelIconChip),
                new PropertyMetadata("", (d, e) => ((PixelIconChip)d).GlyphText.Text = (string)e.NewValue));

        public string Tint
        {
            get => (string)GetValue(TintProperty);
            set => SetValue(TintProperty, value);
        }
        public static readonly DependencyProperty TintProperty =
            DependencyProperty.Register(nameof(Tint), typeof(string), typeof(PixelIconChip),
                new PropertyMetadata("#111111", (d, e) => {
                    var c = (PixelIconChip)d;
                    var hex = ((string)e.NewValue).TrimStart('#');
                    c.GlyphText.Foreground = new SolidColorBrush(Color.FromRgb(
                        Convert.ToByte(hex.Substring(0, 2), 16),
                        Convert.ToByte(hex.Substring(2, 2), 16),
                        Convert.ToByte(hex.Substring(4, 2), 16)));
                }));
    }
}
