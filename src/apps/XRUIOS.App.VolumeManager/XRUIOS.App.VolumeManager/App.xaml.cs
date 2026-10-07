using System.Windows;
using XRUIOS.Apps.Shared;

namespace XRUIOS.App.VolumeManager
{
    public sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            SceneCycler.Register("Mixer", () => new MainPage());
            RootVisual = SceneCycler.Initial() ?? new MainPage();
        }
    }
}
