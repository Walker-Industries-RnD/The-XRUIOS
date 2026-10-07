using System.Windows.Controls;
using XRUIOS.App.Clipboard.Controls;

namespace XRUIOS.App.Clipboard
{
    public partial class WideView : Page
    {
        public WideView()
        {
            InitializeComponent();

            for (var i = 0; i < 8; i++)
                GroupsPanel.AddCard(new GroupCard("Clipboard Group", "99 Items, 99 Subcategories"));

            for (var i = 0; i < 12; i++)
                TitlesPanel.AddCard(new TitleRow("Clipboard Group"));

            var covers = new (string title, string tint)[]
            {
                ("Cyberpunk 2077", "#F0DA1F"), ("www.website.com", "#B0B0B0"),
                ("Nier Automata",  "#3C4F3B"), ("Armored Core 6: Flames o...", "#8A5B7C"),
                ("Mark Of The Slayer", "#111111"), ("On-Lyne Cover 1", "#D67E7E"),
            };
            foreach (var (title, tint) in covers) CoversPanel.AddCover(new CoverTile(title, tint));
        }
    }
}
