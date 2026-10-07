using System;
using System.Windows.Controls;
using XRUIOS.Apps.Shared;
using XRUIOS.Apps.Shared.Holes;

namespace XRUIOS.App.Power
{
    public partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            TimeLabel.Text = DateTime.Now.ToString("HH:mm");

            Loaded += async (_, _) =>
            {
                try
                {
                    await AppShell.InitializeAsync("XRUIOS.App.Power");
                    var raw = await PowerControl.BatteryAsync();
                    // Battery hole returns "NN%" or "NN"; extract the number.
                    var digits = new string(System.Linq.Enumerable.ToArray(
                        System.Linq.Enumerable.Where(raw ?? "", char.IsDigit)));
                    if (int.TryParse(digits, out var pct))
                        Battery.Percent = pct;
                }
                catch { /* keep the seeded 90% */ }
            };
        }
    }
}
