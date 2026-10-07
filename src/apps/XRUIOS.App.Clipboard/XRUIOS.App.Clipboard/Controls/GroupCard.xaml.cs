using System.Windows.Controls;

namespace XRUIOS.App.Clipboard.Controls
{
    public partial class GroupCard : UserControl
    {
        public GroupCard() => InitializeComponent();

        public GroupCard(string name, string subtitle) : this()
        {
            NameText.Text     = name;
            SubtitleText.Text = subtitle;
        }
    }
}
