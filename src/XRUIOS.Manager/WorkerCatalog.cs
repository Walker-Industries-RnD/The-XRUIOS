using XRUIOS.Contracts;

namespace XRUIOS.Manager
{
    // The per-class workers the Manager supervises: each maps a worker name + permission group to its
    // executable. Names match XRUIOS.Worker.<Class>, which is also the SecureStore key each publishes.
    internal static class WorkerCatalog
    {
        internal sealed record Entry(string Name, PermissionGroup Group, string ExePath);

        // Short class name + its permission group. The worker's full name is "XRUIOS.Worker.<Short>".
        private static readonly (string Short, PermissionGroup Group)[] Defs =
        {
            ("Songs", PermissionGroup.Media), ("MusicPlayer", PermissionGroup.Media),
            ("MediaAlbum", PermissionGroup.Media), ("Creator", PermissionGroup.Media),
            ("MediaTagger", PermissionGroup.Media), ("RecentlyRecorded", PermissionGroup.Media),

            ("Calendar", PermissionGroup.Time), ("Alarm", PermissionGroup.Time),
            ("Timer", PermissionGroup.Time), ("Stopwatch", PermissionGroup.Time),
            ("Chrono", PermissionGroup.Time),

            ("AreaManager", PermissionGroup.Spatial), ("WorldEvents", PermissionGroup.Spatial),
            ("DataManager", PermissionGroup.Spatial), ("Geo", PermissionGroup.Spatial),
            ("DeviceManager", PermissionGroup.Spatial),

            ("Volume", PermissionGroup.Audio), ("SoundEQ", PermissionGroup.Audio),
            ("ExperimentalVolume", PermissionGroup.Audio),

            ("Theme", PermissionGroup.Interface), ("Notification", PermissionGroup.Interface),
            ("Clipboard", PermissionGroup.Interface), ("Note", PermissionGroup.Interface),

            ("Identity", PermissionGroup.Identity),

            ("SystemInfo", PermissionGroup.System), ("App", PermissionGroup.System),
            ("Processes", PermissionGroup.System),
        };

        // Every worker's short class name (Calendar, Alarm, ...), whether or not its exe is built yet.
        // Used to grant the Diagnostics app one health probe (<Short>.Ping) per worker.
        public static IEnumerable<string> AllShortNames => Defs.Select(d => d.Short);

        // Resolve each worker's built exe. Dev layout: src/workers/XRUIOS.Worker.<X>/bin/<cfg>/net10.0/.
        // Override the root with XRUIOS_WORKERS_ROOT (an installer points this at the deploy folder).
        public static IReadOnlyList<Entry> Discover()
        {
            string exeExt = OperatingSystem.IsWindows() ? ".exe" : "";
            var found = new List<Entry>();
            foreach (var (shortName, group) in Defs)
            {
                string full = "XRUIOS.Worker." + shortName;
                string? exe = ResolveExe(full, exeExt);
                if (exe != null) found.Add(new Entry(full, group, exe));
            }
            return found;
        }

        private static string? ResolveExe(string workerName, string exeExt)
        {
            string fileName = workerName + exeExt;
            var roots = new List<string>();

            string? envRoot = Environment.GetEnvironmentVariable("XRUIOS_WORKERS_ROOT");
            if (!string.IsNullOrEmpty(envRoot)) roots.Add(envRoot);

            // Dev: walk up from the Manager's base directory to the repo, then into src/workers.
            string baseDir = AppContext.BaseDirectory;
            for (int up = 0; up < 8; up++)
            {
                string guess = Path.Combine(baseDir, "src", "workers", workerName, "bin");
                if (Directory.Exists(guess)) roots.Add(guess);
                baseDir = Path.GetFullPath(Path.Combine(baseDir, ".."));
            }

            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;
                var hit = Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).FirstOrDefault();
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
