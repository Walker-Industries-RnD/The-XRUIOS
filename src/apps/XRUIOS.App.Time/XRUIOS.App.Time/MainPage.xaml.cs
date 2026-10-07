using System.Windows.Controls;
using XRUIOS.App.Time.Controls;
using XRUIOS.Apps.Shared;
using XRUIOS.Apps.Shared.Holes;

namespace XRUIOS.App.Time
{
    public partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            for (var i = 0; i < 3; i++)
                Stopwatches.Items.Add(new StopwatchCard("Stopwatch Name", "Lap Time: 00:10:00", "Lap 1"));

            Loaded += async (_, _) =>
            {
                try
                {
                    await AppShell.InitializeAsync("XRUIOS.App.Time");
                    var now = await TimeSource.NowAsync();
                    // Show the live clock in the "12 Active" subtitle slot so the
                    // header stays "Stopwatch" per mockup.
                    if (!string.IsNullOrWhiteSpace(now)) ActiveCountLine.Text = now;
                }
                catch { }
            };
        }
    }
}
