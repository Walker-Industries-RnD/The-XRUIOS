using System.Windows;
using XRUIOS.Apps.Shared;

namespace XRUIOS.App.Gallery
{
    public sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            SceneCycler.Register("Albums", () => new MainPage());
            SceneCycler.Register("Images", () => new ImagesView());
            RootVisual = SceneCycler.Initial() ?? new MainPage();
        }
    }
}
