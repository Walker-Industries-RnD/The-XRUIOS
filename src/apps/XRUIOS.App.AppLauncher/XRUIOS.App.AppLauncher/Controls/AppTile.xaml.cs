using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace XRUIOS.App.AppLauncher.Controls
{
    public partial class AppTile : UserControl
    {
        public AppTile() => InitializeComponent();

        public AppTile(string name, string tintHex) : this()
        {
            NameText.Text = name;
            IconChip.Background = new SolidColorBrush(ParseHex(tintHex));
        }

        private static Color ParseHex(string hex)
        {
            hex = hex.TrimStart('#');
            return Color.FromRgb(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16));
        }
    }
}
