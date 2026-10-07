using System;
using System.Windows.Controls;
using XRUIOS.App.Weather.Controls;
using XRUIOS.Apps.Shared;
using XRUIOS.Apps.Shared.Holes;

namespace XRUIOS.App.Weather
{
    public partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            SeedSample();

            Loaded += async (_, _) =>
            {
                TimeText.Text = DateTime.Now.ToString("h:mmtt").ToUpper();
                try
                {
                    await AppShell.InitializeAsync("XRUIOS.App.Weather");
                    var snap = await WeatherFeed.CurrentAsync();
                    if (snap is null) return;
                    TempText.Text     = snap.TempC + "°C";
                    FeelsText.Text    = "Feels like " + snap.TempC + "°C";
                    LocationText.Text = snap.Location;
                    Hours.Items.Clear();
                    for (var i = 0; i < 7; i++)
                        Hours.Items.Add(new HourRow(snap.Condition, "H " + snap.High + " · L " + snap.Low,
                                                    ((i + 2) % 12 + 1) + "PM", snap.TempC.ToString()));
                }
                catch { }
            };
        }

        private void SeedSample()
        {
            for (var i = 0; i < 7; i++)
                Hours.Items.Add(new HourRow("Cloudy", "%40 Chance of Precip.", "2PM", "25"));
        }
    }
}
