using XRUIOS.Interfaces;
using XRUIOS.Interpreter;

namespace XRUIOS.Manager
{
    // The apps that ship with the XRUIOS, with exactly the capabilities their holes call
    // (src/apps/XRUIOS.Apps.Shared/Holes/*) and the events each one listens for. Every UI app also
    // gets ThemeChanged so it can repaint.
    internal static class AppCatalog
    {
        private sealed record BuiltIn(string Name, string[] ExeNames, string[] Caps, string[] Events);

        private static readonly BuiltIn[] Defs =
        {
            // The console sample is granted the two shareable Calendar capabilities but NOT DeleteEvent,
            // so its last call is refused (see the demo).
            new("sampleapp", new[] { "XRUIOS.SampleApp" }, new[] { "GetEvents", "AddEvent" }, Array.Empty<string>()),

            new("XRUIOS.App.Calendar", Ui("Calendar"), new[] { "ListCalendars", "CreateCalendar", "GetEvents", "AddEvent" }, Array.Empty<string>()),
            new("XRUIOS.App.Gallery", Ui("Gallery"), new[] { "ListAlbums", "CreateAlbum" }, Array.Empty<string>()),
            new("XRUIOS.App.AppLauncher", Ui("AppLauncher"), new[] { "ListApps", "LaunchApp" }, new[] { XruiosEventNames.AppLaunched }),
            new("XRUIOS.App.ThemeEditor", Ui("ThemeEditor"), new[] { "ListThemes", "ApplyTheme", "GetActiveTheme" }, Array.Empty<string>()),
            new("XRUIOS.App.Clipboard", Ui("Clipboard"), new[] { "Recent", "Pin", "Share" }, new[] { XruiosEventNames.ClipboardChanged }),
            new("XRUIOS.App.Notifications", Ui("Notifications"), new[] { "Unread", "Dismiss" },
                new[] { XruiosEventNames.NotificationPosted, XruiosEventNames.NotificationDismissed }),
            new("XRUIOS.App.VolumeManager", Ui("VolumeManager"), new[] { "ListChannels", "SetVolume", "ToggleMute" }, new[] { XruiosEventNames.VolumeChanged }),
            new("XRUIOS.App.Power", Ui("Power"), new[] { "PowerRequest", "BatteryStatus" }, new[] { XruiosEventNames.PowerRequested }),
            new("XRUIOS.App.Weather", Ui("Weather"), new[] { "Current" }, new[] { XruiosEventNames.WeatherUpdated }),
            new("XRUIOS.App.WorldEvents", Ui("WorldEvents"), new[] { "Today" }, new[] { XruiosEventNames.WorldEventAdded }),
            new("XRUIOS.App.Time", Ui("Time"),
                new[] { "Now", "ListAlarms", "AddAlarm", "DeleteAlarm", "StopwatchStart", "StopwatchLap", "StopwatchElapsed", "StopwatchStop" },
                new[] { XruiosEventNames.AlarmFired, XruiosEventNames.AlarmsChanged }),
            new("XRUIOS.App.PocketPal", Ui("PocketPal"), new[] { "Ask", "AssistantHistory" }, new[] { XruiosEventNames.AssistantAnswered }),
            new("XRUIOS.App.Keyboard", Ui("Keyboard"), new[] { "ListKeyboardLayouts", "ApplyKeyboardLayout" }, new[] { XruiosEventNames.KeyboardLayoutChanged }),
        };

        // Grants older builds made that no longer belong to an app. Revoked once on startup.
        // ("Recent" was PocketPal's history before it was renamed; it routed to the Clipboard worker.)
        public static readonly Dictionary<string, string[]> LegacyGrants = new()
        {
            ["XRUIOS.App.PocketPal"] = new[] { "Recent" },
        };

        // The UI apps run from their Photino head; the plain name covers a single-project app.
        private static string[] Ui(string shortName) =>
            new[] { $"XRUIOS.App.{shortName}.Photino", $"XRUIOS.App.{shortName}" };

        /// <summary>Every built-in app as a manifest, with its executable resolved when it's built.</summary>
        public static IEnumerable<AppManifest> BuiltIns()
        {
            foreach (var d in Defs)
            {
                var events = d.Name == "sampleapp" ? d.Events : d.Events.Append(XruiosEventNames.ThemeChanged).ToArray();
                yield return new AppManifest(d.Name, ResolveExe(d.Name, d.ExeNames), d.Caps, events);
            }

            // The Diagnostics harness gets every worker's health probe plus Calendar's GetEvents/AddEvent so
            // it can prove a real round-trip. DeleteEvent is withheld on purpose: the harness checks that an
            // ungranted call is refused.
            var diagCaps = WorkerCatalog.AllShortNames.Select(s => s + ".Ping").Concat(new[] { "GetEvents", "AddEvent" }).ToArray();
            yield return new AppManifest("diagnostics", ResolveExe("diagnostics", new[] { "XRUIOS.Diagnostics" }), diagCaps, null);
        }

        // Installed layout: XRUIOS_APPS_ROOT/<anything>/<exe>. Dev layout: walk up from the Manager to the
        // repo and look under src/apps/<name> and src/<exe> build output.
        private static string? ResolveExe(string appName, string[] exeNames)
        {
            string ext = OperatingSystem.IsWindows() ? ".exe" : "";
            var roots = new List<string>();

            string? envRoot = Environment.GetEnvironmentVariable("XRUIOS_APPS_ROOT");
            if (!string.IsNullOrEmpty(envRoot)) roots.Add(envRoot);

            string dir = AppContext.BaseDirectory;
            for (int up = 0; up < 8; up++)
            {
                roots.Add(Path.Combine(dir, "src", "apps", appName));
                foreach (var exe in exeNames) roots.Add(Path.Combine(dir, "src", exe, "bin"));
                dir = Path.GetFullPath(Path.Combine(dir, ".."));
            }

            foreach (var root in roots.Where(Directory.Exists))
                foreach (var exe in exeNames)
                {
                    var hit = Directory.EnumerateFiles(root, exe + ext, SearchOption.AllDirectories)
                        .FirstOrDefault(p => p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) || root == envRoot);
                    if (hit != null) return hit;
                }
            return null;
        }

        /// <summary>The Notary name an app's approved folder is stored under.</summary>
        public static WorkerDescriptor Descriptor(AppManifest app) =>
            new() { Name = app.AppId, ExecutablePath = app.Exe! };
    }
}
