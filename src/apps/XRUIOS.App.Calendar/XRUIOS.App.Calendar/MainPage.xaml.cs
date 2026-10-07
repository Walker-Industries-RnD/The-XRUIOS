using System;
using System.Windows.Controls;
using XRUIOS.App.Calendar.Controls;
using XRUIOS.Apps.Shared;

namespace XRUIOS.App.Calendar
{
    public partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            DateHeader.Text = FormatDate(DateTime.Today);

            // Start with placeholder cards so the strip has something to draw
            // while we wait on the worker. FetchEventsAsync will replace them
            // (or leave them, if the broker isn't reachable).
            SeedSampleCards();

            Loaded += async (_, _) =>
            {
                try
                {
                    await AppShell.InitializeAsync("XRUIOS.App.Calendar");
                    StatusLine.Text = "connected";
                    await FetchTodayAsync();
                }
                catch (Exception)
                {
                    StatusLine.Text = "offline";
                }
            };
        }

        private void SeedSampleCards()
        {
            Events.Items.Clear();
            for (var i = 0; i < 5; i++)
            {
                Events.Items.Add(new EventCard(
                    title: "Release Party",
                    startEnd: "00:00xm - 00:00xm",
                    description: "Introducing the NerveLink, a revolutionary Full-Dive Virtual Reality headset inspired by the technology seen in Sword Art Online. NerveLink uses advanced neural interface technology to create a seamless connection between your mind and a virtual world, offering experiences never before possible.",
                    importance: "High Importance",
                    repeats: "Repeats: Never",
                    isSelected: i == 2));
            }
        }

        // The Calendar worker's GetEvents returns "HH:mm Summary; HH:mm Summary; ...".
        // Parse that into cards; if the worker only returns the "No events" line,
        // keep the sample cards so the layout still reads.
        private async System.Threading.Tasks.Task FetchTodayAsync()
        {
            var iso = DateTime.Today.ToString("yyyy-MM-dd");
            string raw;
            try
            {
                raw = await AppShell.Broker.InvokeAsync<string>("GetEvents", new()
                {
                    ["day"] = iso,
                });
            }
            catch { StatusLine.Text = "get-events failed"; return; }

            if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("No events", StringComparison.OrdinalIgnoreCase))
            {
                StatusLine.Text = "no events today";
                return;
            }

            var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries);
            EventCountLine.Text = parts.Length + " Upcoming Events";
            Events.Items.Clear();
            var selectedIdx = parts.Length / 2;
            for (var i = 0; i < parts.Length; i++)
            {
                var entry = parts[i].Trim();
                var sp = entry.IndexOf(' ');
                var time = sp > 0 ? entry.Substring(0, sp) : "";
                var summary = sp > 0 ? entry.Substring(sp + 1) : entry;
                Events.Items.Add(new EventCard(
                    title: summary,
                    startEnd: time,
                    description: "",
                    importance: "",
                    repeats: "",
                    isSelected: i == selectedIdx));
            }
            StatusLine.Text = parts.Length + " events";
        }

        // "December 20th, 2022" style — ordinal suffix + full month name.
        private static string FormatDate(DateTime d)
        {
            int day = d.Day;
            string suffix =
                (day % 100 is >= 11 and <= 13) ? "th"
                : (day % 10) switch
                {
                    1 => "st", 2 => "nd", 3 => "rd", _ => "th",
                };
            return d.ToString("MMMM ") + day + suffix + d.ToString(", yyyy");
        }
    }
}
