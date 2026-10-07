using EclipseProject;
using XRUIOS.Interfaces;
using XRUIOS.Barebones.Functions;
using XRUIOS.Interpreter;

namespace XRUIOS.Worker.App
{
    // "What can I open, and open it." Two sources: apps registered with the XRUIOS (AppClass
    // manifests, id "xruios:<Identifier>", launched from their EntryPoint) and everything the OS itself
    // has installed (Start Menu / .desktop entries, via the interpreter). ListApps/LaunchApp are what
    // the AppLaunchTarget hole calls. Launch is by id only - never an arbitrary path from the caller.
    public static class AppCapabilities
    {
        private const string XruiosPrefix = "xruios:";

        private static IAppInterpreter Apps => XruiosInterpreter.Current.Apps;

        [SeaOfDirac("App.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.App: " + input;

        /// <summary>"id|name|iconPath;..." - XRUIOS apps first, then the OS's installed apps.</summary>
        [SeaOfDirac("ListApps", new string[0], typeof(string))]
        public static async Task<string> ListApps()
        {
            var registered = await AppClass.GetApp();
            var rows = registered
                .Where(m => m is not null)
                .Select(m => new InstalledApp(XruiosPrefix + m.Identifier, m.Name, "", m.EntryPoint))
                .Concat(Apps.ListInstalled());
            return Wire.Apps(rows);
        }

        /// <summary>Returns "launched &lt;name&gt;" (with the pid when the OS reports one).</summary>
        [SeaOfDirac("LaunchApp", new[] { "appId" }, typeof(string), typeof(string))]
        public static async Task<string> LaunchApp(string appId)
        {
            int pid;
            string name;
            if (appId.StartsWith(XruiosPrefix, StringComparison.Ordinal))
            {
                var manifest = await AppClass.GetApp(appId[XruiosPrefix.Length..]);
                pid = Apps.LaunchRegistered(manifest.EntryPoint);
                name = manifest.Name;
            }
            else
            {
                pid = Apps.Launch(appId);
                name = Apps.ListInstalled().FirstOrDefault(a => a.Id == appId)?.Name ?? appId;
            }
            WorkerEvents.Publish(XruiosEventNames.AppLaunched, EventJson.Write(new AppLaunch(appId, name, pid)));
            return pid > 0 ? $"launched {name} (pid {pid})" : $"launched {name}";
        }
    }
}
