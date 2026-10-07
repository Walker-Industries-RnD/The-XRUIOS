using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace XRUIOS.App.Power.Controls
{
    /// <summary>
    /// One rounded-square action tile on the Power widget: icon (rendered
    /// from a small vector Path) + label. Stays a Button subclass so click
    /// events reach the parent widget naturally. Icon is picked by a Glyph
    /// property: "power" | "sleep" | "logout".
    /// </summary>
    public sealed class PowerAction : Button
    {
        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(PowerAction),
                new PropertyMetadata("", (d, _) => ((PowerAction)d).Rebuild()));

        public string Glyph
        {
            get => (string)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }
        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(PowerAction),
                new PropertyMetadata("power", (d, _) => ((PowerAction)d).Rebuild()));

        public PowerAction()
        {
            Background = Brushes.Transparent;
            BorderThickness = new Thickness(0);
            Padding = new Thickness(0);
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            HorizontalContentAlignment = HorizontalAlignment.Center;
            Margin = new Thickness(10);
            Rebuild();
        }

        // Simple 24-unit path data for each glyph.
        private static string DataFor(string kind) => kind switch
        {
            "sleep"  => "M4,12 A8,8 0 1 0 16,4 A6,6 0 1 1 4,12 Z",                  // crescent moon
            "logout" => "M3,3 L3,21 L13,21 L13,17 L5,17 L5,7 L13,7 L13,3 Z M15,7 L20,12 L15,17 L15,14 L9,14 L9,10 L15,10 Z", // arrow-out
            _         => "M12,3 L12,13 M6.3,5.7 A9,9 0 1 0 17.7,5.7", // power circle+I
        };

        private void Rebuild()
        {
            var shadow = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0)),
                CornerRadius = new CornerRadius(16),
                Margin = new Thickness(2, 3, 0, 0),
            };
            var chip = new Border
            {
                Width = 130, Height = 130,
                Background = new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xE5)),
                BorderThickness = new Thickness(1),
                Child = new Path
                {
                    Data = Geometry.Parse(DataFor(Glyph)),
                    Stretch = Stretch.Uniform,
                    Stroke = Glyph == "power"
                        ? new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11))
                        : null,
                    Fill = Glyph == "power"
                        ? null
                        : new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11)),
                    StrokeThickness = Glyph == "power" ? 2 : 0,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Width = 60, Height = 60,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            var chipHost = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
            chipHost.Children.Add(shadow);
            chipHost.Children.Add(chip);

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(chipHost);
            stack.Children.Add(new TextBlock
            {
                Text = Label,
                FontSize = 20,
                FontWeight = FontWeights.Light,
                Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0),
            });
            Content = stack;
        }
    }
}
