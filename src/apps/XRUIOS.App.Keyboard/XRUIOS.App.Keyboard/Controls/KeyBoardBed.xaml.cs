using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XRUIOS.App.Keyboard.Controls
{
    public partial class KeyBoardBed : UserControl
    {
        public KeyBoardBed() => InitializeComponent();

        public void LoadStandardLayout()
        {
            var rows = new[]
            {
                "1234567890",
                "qwertyuiop",
                "asdfghjkl",
                "zxcvbnm",
            };
            for (var r = 0; r < 5; r++)
                KeyHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            AddRow(0, rows[0].ToCharArray());
            AddRow(1, rows[1].ToCharArray());
            AddRow(2, rows[2].ToCharArray());

            // Shift + ZXCV-M + Backspace
            EnsureCols(10);
            var shift = MakeKey("⇧", "#DDDDDD"); Grid.SetRow(shift, 3); Grid.SetColumn(shift, 0); KeyHost.Children.Add(shift);
            for (var i = 0; i < rows[3].Length; i++)
            {
                var k = MakeKey(rows[3][i].ToString());
                Grid.SetRow(k, 3); Grid.SetColumn(k, i + 1); KeyHost.Children.Add(k);
            }
            var back = MakeKey("⌫", "#DDDDDD"); Grid.SetRow(back, 3); Grid.SetColumn(back, 9); KeyHost.Children.Add(back);

            // Row 4: modes + space bar + enter
            var ctrl  = MakeKey("&123", "#DDDDDD"); Grid.SetRow(ctrl, 4);  Grid.SetColumn(ctrl, 0); KeyHost.Children.Add(ctrl);
            var space = MakeKey("");                Grid.SetRow(space, 4); Grid.SetColumn(space, 1); Grid.SetColumnSpan(space, 7); KeyHost.Children.Add(space);
            var enter = MakeKey("⏎", "#DDDDDD"); Grid.SetRow(enter, 4); Grid.SetColumn(enter, 8); Grid.SetColumnSpan(enter, 2); KeyHost.Children.Add(enter);
        }

        private void EnsureCols(int cols)
        {
            while (KeyHost.ColumnDefinitions.Count < cols)
                KeyHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        private void AddRow(int row, char[] chars)
        {
            EnsureCols(chars.Length);
            for (var i = 0; i < chars.Length; i++)
            {
                var k = MakeKey(chars[i].ToString());
                Grid.SetRow(k, row);
                Grid.SetColumn(k, i);
                KeyHost.Children.Add(k);
            }
        }

        private static Border MakeKey(string label, string bgHex = "#FFFFFF")
        {
            var hex = bgHex.TrimStart('#');
            var bg = Color.FromRgb(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16));
            return new Border
            {
                Background = new SolidColorBrush(bg),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(4),
                Child = new TextBlock
                {
                    Text = label,
                    FontSize = 16,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
        }
    }
}
