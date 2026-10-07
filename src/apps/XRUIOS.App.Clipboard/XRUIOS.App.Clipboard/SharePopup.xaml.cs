using System.Windows.Controls;

namespace XRUIOS.App.Clipboard
{
    /// <summary>
    /// Companion window for the main Clipboard widget. Opened by the Photino
    /// host as its own PhotinoWindow when the user asks to share a clip.
    /// Sized to the mockup (299x246) and wrapped in a Viewbox so it scales.
    /// </summary>
    public partial class SharePopup : Page
    {
        public SharePopup() => InitializeComponent();
    }
}
