using System.Windows.Controls;
using XRUIOS.App.WorldEvents.Controls;
using XRUIOS.Apps.Shared;
using XRUIOS.Apps.Shared.Holes;

namespace XRUIOS.App.WorldEvents
{
    public partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            for (var i = 0; i < 7; i++)
                Events.Items.Add(new EventRow("XRUIOS World Event Happened", "From Headset @ 00:00xm"));

            Loaded += async (_, _) =>
            {
                try
                {
                    await AppShell.InitializeAsync("XRUIOS.App.WorldEvents");
                    var stories = await WorldEventsFeed.TodayAsync();
                    if (stories.Count == 0) return;
                    Events.Items.Clear();
                    foreach (var s in stories)
                        Events.Items.Add(new EventRow(s.Headline, s.Source + " · " + s.Region));
                }
                catch { }
            };
        }
    }
}
