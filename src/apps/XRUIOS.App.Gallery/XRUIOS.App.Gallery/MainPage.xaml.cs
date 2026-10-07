using System.Windows.Controls;
using XRUIOS.App.Gallery.Controls;
using XRUIOS.Apps.Shared;
using XRUIOS.Apps.Shared.Holes;

namespace XRUIOS.App.Gallery
{
    public partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();

            // Sample albums with real crops so the tiles look 1:1 with the
            // mockup out of the box; the code below reuses these two covers
            // as placeholders across the grid.
            var covers = new[] { "assets/album1.png", "assets/album2.png" };
            for (var i = 0; i < 8; i++)
                Albums.Add(new AlbumCard(
                    title: "Album Name",
                    subtitle: "280 Images, 10 Videos",
                    imageAsset: covers[i % covers.Length]));

            Loaded += async (_, _) =>
            {
                try
                {
                    await AppShell.InitializeAsync("XRUIOS.App.Gallery");
                    var albums = await MediaAlbumPicker.ListAsync();
                    if (albums.Count == 0) return;
                    foreach (var a in albums)
                        Albums.Add(new AlbumCard(a.Title, a.Count + " items"));
                }
                catch { }
            };
        }
    }
}
