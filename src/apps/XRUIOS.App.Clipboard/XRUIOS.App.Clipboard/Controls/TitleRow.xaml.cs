using System.Windows.Controls;

namespace XRUIOS.App.Clipboard.Controls
{
    public partial class TitleRow : UserControl
    {
        public TitleRow() => InitializeComponent();
        public TitleRow(string label) : this() => LabelText.Text = label;
    }
}
