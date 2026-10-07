using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XRUIOS.App.Gallery.Controls
{
    public partial class AlbumCard : UserControl
    {
        public AlbumCard() => InitializeComponent();

        // Album card (cover + title + subtitle) OR photo-only tile (just cover,
        // captions collapsed). Optional imageAsset is a wwwroot-relative PNG
        // path; if given, it replaces the tinted background.
        public AlbumCard(string? title = null, string? subtitle = null,
                         string tint = "#B0B0B0", bool isPhoto = false,
                         string? imageAsset = null) : this()
        {
            Cover.Background = new SolidColorBrush(ParseHex(tint));
            if (isPhoto) Cover.Height = 160;

            if (imageAsset != null)
            {
                CoverImage.Source = new BitmapImage(new Uri(imageAsset, UriKind.Relative));
                CoverImage.Visibility = Visibility.Visible;
            }

            if (title == null)    TitleText.Visibility    = Visibility.Collapsed;
            else                  TitleText.Text          = title;

            if (subtitle == null) SubtitleText.Visibility = Visibility.Collapsed;
            else                  SubtitleText.Text       = subtitle;
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
