using System;
using System.Windows.Controls;
using XRUIOS.App.AppLauncher.Controls;
using XRUIOS.Apps.Shared;
using XRUIOS.Apps.Shared.Holes;

namespace XRUIOS.App.AppLauncher
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
                    await AppShell.InitializeAsync("XRUIOS.App.AppLauncher");
                    var apps = await AppLaunchTarget.ListAsync();
                    if (apps.Count == 0) return;
                    Tiles.Clear();
                    foreach (var a in apps)
                        Tiles.Add(new AppTile(a.DisplayName, "#111111"));
                }
                catch { /* stay on sample tiles */ }
            };
        }

        private void SeedSample()
        {
            var seed = new (string name, string tint)[]
            {
                ("APB Reloaded", "#111111"), ("DayZ", "#3C3F45"), ("SCP: Secret L...", "#0F0F0F"),
                ("Half Life 2", "#221E17"), ("Warframe", "#F2F5F8"),
                ("LoveChoice", "#8BA6E0"), ("Team Fortress 2", "#B5651D"), ("Ultrakill", "#111111"),
                ("Halo Infinite", "#4E6F52"), ("WI Home", "#F5F5F5"),
                ("Armored Core 6: F...", "#B4B4B4"), ("Left 4 Dead 2", "#3E2A16"), ("Toram Online", "#B8CFE2"),
                ("Doom Eternal", "#111111"), ("XRUIOS Helper", "#1B1B1B"),
            };
            foreach (var (name, tint) in seed) Tiles.Add(new AppTile(name, tint));
        }
    }
}
