using System.Windows.Controls;
using XRUIOS.App.PocketPal.Controls;
using XRUIOS.Apps.Shared;
using XRUIOS.Apps.Shared.Holes;

namespace XRUIOS.App.PocketPal
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
                    await AppShell.InitializeAsync("XRUIOS.App.PocketPal");
                    var recent = await PocketPalIntent.RecentAsync();
                    if (string.IsNullOrWhiteSpace(recent)) return;
                    // Recent replies come back as "Speaker | body" rows joined by "; ".
                    Replies.Items.Clear();
                    foreach (var row in recent.Split(';', System.StringSplitOptions.RemoveEmptyEntries))
                    {
                        var parts = row.Split('|', 2);
                        if (parts.Length == 2)
                            Replies.Items.Add(new SpeakerReply(parts[0].Trim(), parts[1].Trim()));
                    }
                }
                catch { }
            };
        }

        private void SeedSample()
        {
            Replies.Items.Add(new SpeakerReply("G1 Michigan",
                "Gun 13! We're gonna give these orphans something to REALLY cry about!"));
            Replies.Items.Add(new SpeakerReply("Iguazu",
                "Damn freelancers, i'm gonna kill more orphans than you! Just stick to the sidelines and watch."));
            Replies.Items.Add(new SpeakerReply("Ayre",
                "This is the second worst thing that's ever happened to these orphans."));
        }
    }
}
