using OpenSilver.Photino;
using Photino.NET;

namespace XRUIOS.App.Time.Photino
{
    // Time - fullscreen (1920x1080). Standard OpenSilver+Photino boot: fluent
    // PhotinoWindow, ConfigureOpenSilver hands the App class to the OpenSilver
    // runtime, then Load points at the wwwroot bundle the template ships.
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            const string title = "Time";

            var window = new PhotinoWindow()
                .SetTitle(title)
                .SetUseOsDefaultSize(false)
                .SetSize(1920, 1080)
                .SetResizable(true)
                .Center()
                .SetLogVerbosity(0)
                .ConfigureOpenSilver<App>()
                .Load("wwwroot/index.html");

            window.WaitForClose();
        }
    }
}
