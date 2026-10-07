using OpenSilver.Photino;
using Photino.NET;

namespace XRUIOS.App.VolumeManager.Photino
{
    // Volume Manager - widget (899x893). Chromeless, transparent, always-on-top so it
    // floats over the desktop. The XAML side (WidgetChrome inside MainPage)
    // draws the card and the drag strip; we react to its "drag-start" IPC
    // message by asking Photino to follow the cursor. Pin flips TopMost; close
    // hides the window (the tray reopens widgets - they never fully exit).
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            const string title = "Volume Manager";

            var window = new PhotinoWindow()
                .SetTitle(title)
                .SetUseOsDefaultSize(false)
                .SetSize(899, 893)
                .SetResizable(false)
                .SetChromeless(true)
                .SetTransparent(true)
                .SetTopMost(true)
                .Center()
                .SetLogVerbosity(0)
                .ConfigureOpenSilver<App>()
                .Load("wwwroot/index.html");

            window.RegisterWebMessageReceivedHandler((sender, message) =>
            {
                switch (message)
                {
                    case "drag-start":
                        // MoveTo with the current position begins a follow-cursor loop.
                        window.MoveTo(window.Left, window.Top);
                        break;
                    case "pin-on":
                        window.SetTopMost(true);
                        break;
                    case "pin-off":
                        window.SetTopMost(false);
                        break;
                    case "close":
                        window.Close();
                        break;
                }
            });

            window.WaitForClose();
        }
    }
}
