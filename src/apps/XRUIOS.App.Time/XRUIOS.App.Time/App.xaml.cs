using System.Windows;
using XRUIOS.Apps.Shared;

namespace XRUIOS.App.Time
{
    public sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            SceneCycler.Register("Stopwatch", () => new MainPage());
            RootVisual = SceneCycler.Initial() ?? new MainPage();
        }
    }
}
