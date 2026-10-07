using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XRUIOS.App.Calendar.Controls
{
    public partial class MonthGrid : UserControl
    {
        private const int Rows = 5;
        private const int Cols = 7;

        public MonthGrid()
        {
            InitializeComponent();
            for (var c = 0; c < Cols; c++)
                CellHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var r = 0; r < Rows; r++)
                CellHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            for (var r = 0; r < Rows; r++)
                for (var c = 0; c < Cols; c++)
                {
                    var cell = MakeCell(selected: r == 1 && c == 3, alt: (r + c) % 2 == 0);
                    Grid.SetRow(cell, r);
                    Grid.SetColumn(cell, c);
                    CellHost.Children.Add(cell);
                }
        }

        private static UIElement MakeCell(bool selected, bool alt)
        {
            var bg = selected
                ? new SolidColorBrush(Color.FromRgb(0x3F, 0x8A, 0xD1))
                : (alt ? new SolidColorBrush(Color.FromRgb(0xEA, 0xEA, 0xEA))
                       : new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0)));
            var ink = selected
                ? new SolidColorBrush(Colors.White)
                : new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));

            var stack = new StackPanel { Margin = new Thickness(12, 8, 12, 8) };
            stack.Children.Add(new TextBlock
            {
                Text = "29", FontSize = 24, FontWeight = FontWeights.Light,
                Foreground = ink, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 0, 6),
            });
            stack.Children.Add(new TextBlock { Text = "Calendar Event", Foreground = ink, FontSize = 12, FontWeight = FontWeights.Light });
            stack.Children.Add(new TextBlock { Text = "Calendar Event", Foreground = ink, FontSize = 12, FontWeight = FontWeights.Light });
            stack.Children.Add(new TextBlock { Text = "Do A Thing...",  Foreground = ink, FontSize = 12, FontWeight = FontWeights.Light });
            stack.Children.Add(new TextBlock { Text = "+12 More",       Foreground = ink, FontSize = 12, FontWeight = FontWeights.Light, Margin = new Thickness(0, 4, 0, 0) });

            return new Border
            {
                Background = bg,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xE5)),
                BorderThickness = new Thickness(0.5),
                Child = stack,
            };
        }
    }
}
