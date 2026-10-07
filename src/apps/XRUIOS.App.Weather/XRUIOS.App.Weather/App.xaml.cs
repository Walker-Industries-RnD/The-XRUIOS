using System.Windows;
using XRUIOS.Apps.Shared;

namespace XRUIOS.App.Weather
{
    public sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            SceneCycler.Register("Weather", () => new MainPage());
            RootVisual = SceneCycler.Initial() ?? new MainPage();
        }
    }
}
