using System.Windows.Controls;
using XRUIOS.App.VolumeManager.Controls;
using XRUIOS.Apps.Shared;
using XRUIOS.Apps.Shared.Holes;

namespace XRUIOS.App.VolumeManager
{
    public partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            for (var i = 0; i < 5; i++)
                Channels.Items.Add(new ChannelRow("Alien Manifestation - Keigo Ho...", 70));

            Loaded += async (_, _) =>
            {
                try
                {
                    await AppShell.InitializeAsync("XRUIOS.App.VolumeManager");
                    var chs = await VolumeMixer.ListAsync();
                    if (chs.Count == 0) return;
                    Channels.Items.Clear();
                    foreach (var c in chs)
                        Channels.Items.Add(new ChannelRow(c.Name, c.Volume));
                }
                catch { }
            };
        }
    }
}
