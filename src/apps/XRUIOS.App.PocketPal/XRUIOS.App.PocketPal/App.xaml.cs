using System.Windows;
using XRUIOS.Apps.Shared;

namespace XRUIOS.App.PocketPal
{
    public sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            SceneCycler.Register("Chat", () => new MainPage());
            RootVisual = SceneCycler.Initial() ?? new MainPage();
        }
    }
}
