using System.Windows.Controls;
using XRUIOS.App.Clipboard.Controls;
using XRUIOS.Apps.Shared;
using XRUIOS.Apps.Shared.Holes;

namespace XRUIOS.App.Clipboard
{
    public partial class MainPage : Page
    {
        // Seed emoji categories from a small local palette so the design reads
        // without pulling the real ClipboardHistory hole yet. Sizes match the
        // 7-across grid in the mockup.
        private static readonly string[][] SampleGroups =
        {
            new[] { "\U0001F600", "\U0001F604", "\U0001F60A", "\U0001F60E", "\U0001F929", "\U0001F914", "\U0001F644",
                    "\U0001F60F", "\U0001F60D", "\U0001F970", "\U0001F62D", "\U0001F633", "\U0001F631", "\U0001F971",
                    "\U0001F97A", "\U0001F92F", "\U0001F92E", "\U0001F975" },
            new[] { "\U0001F44D", "\U0001F44E", "\U0001F44A", "\U0001F91D", "\U0001F64F", "\U0001F44F", "\U0001F44B",
                    "\U0001F91E", "✌️", "\U0001F91F", "\U0001F918", "\U0001F919", "\U0001F448", "\U0001F449",
                    "\U0001F446", "\U0001F447", "\U0001F595", "\U0001F44C", "\U0001F91B", "\U0001F91C" },
            new[] { "\U0001F4BB", "\U0001F5A5", "\U0001F5B1", "⌨️", "\U0001F4BE", "\U0001F4BF", "\U0001F4F1" },
        };

        public MainPage()
        {
            InitializeComponent();

            foreach (var glyphs in SampleGroups)
                EmojiGroups.Children.Add(new EmojiGroup("Clipboard Group", glyphs));

            // Sample media thumbs — colored blocks stand in until we pipe real
            // clipboard images through. Sizes mirror the masonry in the mockup.
            foreach (var (w, h, tint) in new (double, double, string)[]
                     {
                         (300, 200, "#3E4A55"), (170, 140, "#22334B"),
                         (170, 140, "#3A1E4A"), (170, 140, "#2A4744"),
                         (60,  60,  "#123456"), (60,  60,  "#4B2222"), (60,  60,  "#22334B"),
                         (170, 190, "#513330"), (220, 190, "#252525"),
                         (170, 190, "#443322"), (170, 190, "#332244"),
                         (300, 140, "#111111"), (270, 140, "#8B1A1A"), (200, 140, "#402060"),
                     })
                Media.Children.Add(new MediaTile(w, h, tint));

            Loaded += async (_, _) =>
            {
                try
                {
                    await AppShell.InitializeAsync("XRUIOS.App.Clipboard");
                    // Fire the round-trip so the app is real. Rendering
                    // recent clipboard items into the panels is a scene we
                    // haven't wired yet (needs a per-item preview slot);
                    // the call proves the broker connection is live.
                    _ = await ClipboardHistory.RecentAsync();
                }
                catch { }
            };
        }
    }
}
