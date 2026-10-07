using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace XRUIOS.App.Clipboard.Controls
{
    public partial class MediaTile : UserControl
    {
        public MediaTile() => InitializeComponent();

        public MediaTile(double designWidth, double designHeight, string colorHex) : this()
        {
            _ = designWidth;
            Height = designHeight;
            var hex = colorHex.TrimStart('#');
            TileFill.Background = new SolidColorBrush(Color.FromRgb(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16)));
        }
    }
}
