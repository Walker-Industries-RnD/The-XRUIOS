using System.Windows.Controls;

namespace XRUIOS.App.Notifications.Controls
{
    public partial class NotificationRow : UserControl
    {
        public NotificationRow() => InitializeComponent();

        public NotificationRow(string source, string title, string body, string time) : this()
        {
            SourceText.Text = source;
            TitleText.Text  = title;
            BodyText.Text   = body;
            TimeText.Text   = time;
        }
    }
}
