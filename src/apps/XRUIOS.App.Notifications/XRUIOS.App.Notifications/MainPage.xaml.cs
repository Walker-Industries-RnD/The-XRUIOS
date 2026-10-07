using System.Windows.Controls;
using XRUIOS.App.Notifications.Controls;
using XRUIOS.Apps.Shared;
using XRUIOS.Apps.Shared.Holes;

namespace XRUIOS.App.Notifications
{
    public partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            SeedSample();

            Loaded += async (_, _) =>
            {
                try
                {
                    await AppShell.InitializeAsync("XRUIOS.App.Notifications");
                    var feed = await NotificationFeed.UnreadAsync();
                    if (feed.Count == 0) return;
                    Feed.Items.Clear();
                    foreach (var n in feed)
                        Feed.Items.Add(new NotificationRow(n.Source, n.Title, n.Body, n.Timestamp));
                    MoreLabel.Text = "+" + feed.Count + " Notifications";
                }
                catch { }
            };
        }

        private void SeedSample()
        {
            for (var i = 0; i < 4; i++)
                Feed.Items.Add(new NotificationRow(
                    source: "YuukOS Manager",
                    title:  "System Update Available",
                    body:   "YuukOS 2.1 is ready to install with new spatial apps, performance improvements and security updates.",
                    time:   "Now"));
        }
    }
}
