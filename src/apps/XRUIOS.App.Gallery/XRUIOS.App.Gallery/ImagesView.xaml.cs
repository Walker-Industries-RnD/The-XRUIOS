using System.Windows.Controls;
using XRUIOS.App.Gallery.Controls;

namespace XRUIOS.App.Gallery
{
    public partial class ImagesView : Page
    {
        public ImagesView()
        {
            InitializeComponent();
            var thumbs = new[] { "assets/photo1.png", "assets/photo2.png", "assets/photo3.png" };
            for (var i = 0; i < 8; i++)
                Photos.Add(new AlbumCard(isPhoto: true, imageAsset: thumbs[i % thumbs.Length]));
        }
    }
}
