using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace XRUIOS.App.Clipboard.Controls
{
    public partial class CoverTile : UserControl
    {
        public CoverTile() => InitializeComponent();

        public CoverTile(string caption, string colorHex) : this()
        {
            CaptionText.Text = caption;
            var hex = colorHex.TrimStart('#');
            Cover.Background = new SolidColorBrush(Color.FromRgb(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16)));
        }
    }
}
