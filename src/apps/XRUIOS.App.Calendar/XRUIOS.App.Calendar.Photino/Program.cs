using OpenSilver.Photino;
using Photino.NET;

namespace XRUIOS.App.Calendar.Photino
{
    // Calendar host. Single PhotinoWindow; the app itself owns scene switching
    // (Day / Month) through SceneCycler in App.xaml.cs.
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            const string title = "Calendar";

            new PhotinoWindow()
                .SetTitle(title)
                .SetUseOsDefaultSize(false)
                .SetSize(1920, 1080)
                .SetLocation(new System.Drawing.Point(0, 0))
                .SetResizable(true)
                .SetLogVerbosity(0)
                .ConfigureOpenSilver<App>()
                .Load("wwwroot/index.html")
                .WaitForClose();
        }
    }
}
