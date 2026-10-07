using System.Windows.Controls;

namespace XRUIOS.App.WorldEvents.Controls
{
    public partial class EventRow : UserControl
    {
        public EventRow() => InitializeComponent();

        public EventRow(string title, string origin) : this()
        {
            TitleText.Text  = title;
            OriginText.Text = origin;
        }
    }
}
