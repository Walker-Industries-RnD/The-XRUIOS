using System.Windows;
using XRUIOS.Apps.Shared;

namespace XRUIOS.App.WorldEvents
{
    public sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            SceneCycler.Register("Events", () => new MainPage());
            RootVisual = SceneCycler.Initial() ?? new MainPage();
        }
    }
}
