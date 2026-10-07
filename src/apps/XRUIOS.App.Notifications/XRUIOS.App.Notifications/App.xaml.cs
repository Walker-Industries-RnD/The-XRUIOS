using System.Windows;
using XRUIOS.Apps.Shared;

namespace XRUIOS.App.Notifications
{
    public sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            SceneCycler.Register("Feed", () => new MainPage());
            RootVisual = SceneCycler.Initial() ?? new MainPage();
        }
    }
}
