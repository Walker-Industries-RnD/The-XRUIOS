using System.Windows;
using XRUIOS.Apps.Shared;

namespace XRUIOS.App.Power
{
    public sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            SceneCycler.Register("Power", () => new MainPage());
            RootVisual = SceneCycler.Initial() ?? new MainPage();
        }
    }
}
