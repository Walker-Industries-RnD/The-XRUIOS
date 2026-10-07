using System.Windows.Controls;

namespace XRUIOS.App.PocketPal.Controls
{
    public partial class SpeakerReply : UserControl
    {
        public SpeakerReply() => InitializeComponent();

        // Convenience ctor kept for existing code-behind callers.
        public SpeakerReply(string name, string body) : this()
        {
            NameText.Text = name;
            BodyText.Text = body;
        }
    }
}
