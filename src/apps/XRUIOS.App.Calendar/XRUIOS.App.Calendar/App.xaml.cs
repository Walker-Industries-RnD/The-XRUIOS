using System.Windows;
using XRUIOS.Apps.Shared;

namespace XRUIOS.App.Calendar
{
    public sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Register the two scenes this app ships with. The temp SceneSwitch
            // button drops a "Switch → Month" pill in the corner that cycles.
            SceneCycler.Register("Day",   () => new MainPage());
            SceneCycler.Register("Month", () => new MonthView());

            RootVisual = SceneCycler.Initial() ?? new MainPage();
        }
    }
}
