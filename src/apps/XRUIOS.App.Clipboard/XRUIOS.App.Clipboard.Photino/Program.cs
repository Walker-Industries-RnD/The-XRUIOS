using OpenSilver.Photino;
using Photino.NET;

namespace XRUIOS.App.Clipboard.Photino
{
    // Clipboard host. Single window; SceneCycler in App.xaml.cs cycles the
    // Compact / Wide / Share Bar views.
    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            const string title = "Clipboard";

            new PhotinoWindow()
                .SetTitle(title)
                .SetUseOsDefaultSize(false)
                .SetSize(1245, 766)
                .SetLocation(new System.Drawing.Point(40, 40))
                .SetResizable(true)
                .SetLogVerbosity(0)
                .ConfigureOpenSilver<App>()
                .Load("wwwroot/index.html")
                .WaitForClose();
        }
    }
}
