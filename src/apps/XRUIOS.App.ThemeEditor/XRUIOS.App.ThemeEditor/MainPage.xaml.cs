using System.Windows.Controls;
using XRUIOS.Apps.Shared;
using XRUIOS.Apps.Shared.Holes;

namespace XRUIOS.App.ThemeEditor
{
    public partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();

            Loaded += async (_, _) =>
            {
                try
                {
                    await AppShell.InitializeAsync("XRUIOS.App.ThemeEditor");
                    _ = await ThemePicker.ListAsync();
                    _ = await ThemePicker.GetActiveAsync();
                    // Landing scene shows a hero-plate + preview card; once we
                    // add named text/preview slots the code-behind will push the
                    // active theme's name here.
                }
                catch { }
            };
        }
    }
}
