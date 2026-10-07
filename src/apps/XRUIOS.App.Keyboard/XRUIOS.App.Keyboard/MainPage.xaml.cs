using System.Windows.Controls;
using XRUIOS.Apps.Shared;
using XRUIOS.Apps.Shared.Holes;

namespace XRUIOS.App.Keyboard
{
    public partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            Bed.LoadStandardLayout();

            Loaded += async (_, _) =>
            {
                try
                {
                    await AppShell.InitializeAsync("XRUIOS.App.Keyboard");
                    _ = await KeyboardLayoutPicker.ListAsync();
                    // Layouts arrive but the current MainPage only draws the
                    // standard bed; the picker is what a settings scene would
                    // consume. Keeping the round-trip in so the app is real.
                }
                catch { }
            };
        }
    }
}
