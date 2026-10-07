using System.Windows.Controls;

namespace XRUIOS.App.Clipboard.Controls
{
    public partial class EmojiGroup : UserControl
    {
        public EmojiGroup() => InitializeComponent();

        public EmojiGroup(string label, string[] glyphs) : this()
        {
            LabelText.Text = label;
            foreach (var g in glyphs) Cells.Children.Add(new EmojiCell(g));
        }
    }
}
