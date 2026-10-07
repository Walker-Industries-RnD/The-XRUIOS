using System.Windows;
using XRUIOS.Apps.Shared;

namespace XRUIOS.App.Clipboard
{
    public sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Three windows this app ships with. Temp scene cycler swaps them
            // in the same window until proper navigation lands.
            SceneCycler.Register("Compact",   () => new MainPage());
            SceneCycler.Register("Wide",      () => new WideView());
            SceneCycler.Register("Share Bar", () => new SharePopup());

            RootVisual = SceneCycler.Initial() ?? new MainPage();
        }
    }
}
