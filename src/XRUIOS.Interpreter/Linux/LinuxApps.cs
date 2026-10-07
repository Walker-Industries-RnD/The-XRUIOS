using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;

namespace XRUIOS.Interpreter.Linux
{
    /// <summary>
    /// Installed apps = the freedesktop.org .desktop entries the app menu shows (XDG data dirs plus
    /// flatpak exports), ids per the spec ("org.gnome.Nautilus.desktop"). Launch prefers gtk-launch
    /// (handles Terminal=true and D-Bus activation); otherwise the Exec line is expanded and run
    /// directly - never through a shell.
    /// </summary>
    [SupportedOSPlatform("linux")]
    internal sealed class LinuxApps : IAppInterpreter
    {
        private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);
        private static readonly string[] IconSizes = { "scalable", "512x512", "256x256", "128x128", "96x96", "64x64", "48x48", "32x32" };

        internal sealed record DesktopEntry(string Id, string Path, string Name, string Exec, string Icon, bool Terminal);

        private List<DesktopEntry>? _cache;
        private DateTime _cachedAt;
        private readonly object _gate = new();

        public IReadOnlyList<InstalledApp> ListInstalled()
            => Entries().Select(e => new InstalledApp(e.Id, e.Name, ResolveIcon(e.Icon), e.Path)).ToList();

        public int Launch(string appId)
        {
            var e = Entries().FirstOrDefault(x => x.Id == appId || x.Id == appId + ".desktop")
                    ?? throw new KeyNotFoundException($"No installed app with id '{appId}'.");

            if (Shell.Has("gtk-launch"))
            {
                Shell.RunChecked("gtk-launch", new[] { e.Id }, timeoutMs: 15000);
                return 0;   // gtk-launch hands the app to the session and exits; no child pid to report
            }
            if (e.Terminal)
                throw new PlatformNotSupportedException($"'{e.Name}' is a terminal app; install gtk-launch (libgtk-3-bin) to start it.");

            var argv = ExpandExec(e);
            if (argv.Count == 0) throw new InvalidOperationException($"'{e.Name}' has an empty Exec line.");
            var psi = new ProcessStartInfo(argv[0]) { UseShellExecute = false };
            foreach (var a in argv.Skip(1)) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            return p?.Id ?? 0;
        }

        public int LaunchRegistered(string entryPoint)
        {
            if (!File.Exists(entryPoint)) throw new FileNotFoundException("Registered entry point not found.", entryPoint);
            // UseShellExecute on Linux: runs it if executable, otherwise opens it with xdg-open.
            using var p = Process.Start(new ProcessStartInfo(entryPoint)
            {
                UseShellExecute = true,
                WorkingDirectory = System.IO.Path.GetDirectoryName(entryPoint) ?? "",
            });
            return p?.Id ?? 0;
        }

        // ── discovery ────────────────────────────────────────────────────────────────────────────

        private List<DesktopEntry> Entries()
        {
            lock (_gate)
            {
                if (_cache != null && DateTime.UtcNow - _cachedAt < CacheFor) return _cache;

                var byId = new Dictionary<string, DesktopEntry>(StringComparer.Ordinal);
                var hidden = new HashSet<string>(StringComparer.Ordinal);
                foreach (var dir in ApplicationDirs())
                {
                    if (!Directory.Exists(dir)) continue;
                    IEnumerable<string> files;
                    try { files = Directory.EnumerateFiles(dir, "*.desktop", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }); }
                    catch { continue; }

                    foreach (var file in files)
                    {
                        // Spec: the id is the path under applications/ with '/' turned into '-'.
                        string id = System.IO.Path.GetRelativePath(dir, file).Replace('/', '-');
                        if (byId.ContainsKey(id) || hidden.Contains(id)) continue;   // earlier dirs take precedence

                        var parsed = Parse(file, id);
                        if (parsed is null) hidden.Add(id);   // NoDisplay/Hidden in a user dir masks the system copy
                        else byId[id] = parsed;
                    }
                }

                _cache = byId.Values.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                _cachedAt = DateTime.UtcNow;
                return _cache;
            }
        }

        private static IEnumerable<string> ApplicationDirs()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } dh ? dh : System.IO.Path.Combine(home, ".local", "share");
            string dataDirs = Environment.GetEnvironmentVariable("XDG_DATA_DIRS") is { Length: > 0 } dd ? dd : "/usr/local/share:/usr/share";

            var roots = new List<string> { dataHome };
            roots.AddRange(dataDirs.Split(':', StringSplitOptions.RemoveEmptyEntries));
            // Flatpak exports are normally on XDG_DATA_DIRS already, but not when started outside a login shell.
            roots.Add(System.IO.Path.Combine(home, ".local", "share", "flatpak", "exports", "share"));
            roots.Add("/var/lib/flatpak/exports/share");
            roots.Add("/var/lib/snapd/desktop");

            return roots.Distinct().Select(r => System.IO.Path.Combine(r, "applications"));
        }

        /// <summary>Parse the [Desktop Entry] group; null = not a visible application.</summary>
        internal static DesktopEntry? Parse(string file, string id)
        {
            string[] lines;
            try { lines = File.ReadAllLines(file); } catch { return null; }

            var kv = new Dictionary<string, string>(StringComparer.Ordinal);
            bool inMain = false;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                if (line[0] == '[') { inMain = line == "[Desktop Entry]"; continue; }
                if (!inMain) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                kv.TryAdd(line[..eq].Trim(), line[(eq + 1)..].Trim());
            }

            if (kv.GetValueOrDefault("Type") != "Application") return null;
            if (kv.GetValueOrDefault("NoDisplay") == "true" || kv.GetValueOrDefault("Hidden") == "true") return null;
            if (!kv.TryGetValue("Exec", out var exec) || exec.Length == 0) return null;
            if (kv.TryGetValue("TryExec", out var tryExec) && tryExec.Length > 0 &&
                !(tryExec.StartsWith('/') ? File.Exists(tryExec) : Shell.Has(tryExec))) return null;

            string name = LocalizedName(kv) ?? System.IO.Path.GetFileNameWithoutExtension(file);
            return new DesktopEntry(id, file, name, exec, kv.GetValueOrDefault("Icon") ?? "", kv.GetValueOrDefault("Terminal") == "true");
        }

        private static string? LocalizedName(Dictionary<string, string> kv)
        {
            // LANG=de_DE.UTF-8 → try Name[de_DE], then Name[de], then Name.
            string? lang = (Environment.GetEnvironmentVariable("LC_MESSAGES") ?? Environment.GetEnvironmentVariable("LANG"))?.Split('.', '@')[0];
            if (!string.IsNullOrEmpty(lang))
            {
                if (kv.TryGetValue($"Name[{lang}]", out var full)) return full;
                var shortLang = lang.Split('_')[0];
                if (kv.TryGetValue($"Name[{shortLang}]", out var sh)) return sh;
            }
            return kv.GetValueOrDefault("Name");
        }

        /// <summary>
        /// Expand an Exec line per the Desktop Entry spec: split on unquoted spaces, honour
        /// double-quote escapes, drop file/url field codes (we launch with no files), expand %i/%c/%k.
        /// </summary>
        internal static List<string> ExpandExec(DesktopEntry e)
        {
            var tokens = new List<string>();
            var cur = new StringBuilder();
            bool inQuotes = false, any = false;
            string exec = e.Exec;

            for (int i = 0; i < exec.Length; i++)
            {
                char c = exec[i];
                if (inQuotes)
                {
                    if (c == '\\' && i + 1 < exec.Length && "\"`$\\".Contains(exec[i + 1])) { cur.Append(exec[++i]); continue; }
                    if (c == '"') { inQuotes = false; continue; }
                    cur.Append(c);
                }
                else if (c == '"') { inQuotes = true; any = true; }
                else if (c == ' ' || c == '\t')
                {
                    if (cur.Length > 0 || any) tokens.Add(cur.ToString());
                    cur.Clear(); any = false;
                }
                else cur.Append(c);
            }
            if (cur.Length > 0 || any) tokens.Add(cur.ToString());

            var argv = new List<string>();
            foreach (var t in tokens)
            {
                switch (t)
                {
                    case "%f": case "%F": case "%u": case "%U": case "%d": case "%D": case "%n": case "%N": case "%v": case "%m":
                        continue;
                    case "%i":
                        if (e.Icon.Length > 0) { argv.Add("--icon"); argv.Add(e.Icon); }
                        continue;
                }
                argv.Add(t.Replace("%c", e.Name).Replace("%k", e.Path).Replace("%%", "%"));
            }
            return argv;
        }

        /// <summary>Icon name → file (hicolor theme / pixmaps). Falls back to the bare name for theme lookup.</summary>
        private static string ResolveIcon(string icon)
        {
            if (icon.Length == 0) return "";
            if (icon.StartsWith('/')) return icon;

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var roots = new List<string> { System.IO.Path.Combine(home, ".local", "share"), System.IO.Path.Combine(home, ".local", "share", "flatpak", "exports", "share"), "/var/lib/flatpak/exports/share" };
            roots.AddRange((Environment.GetEnvironmentVariable("XDG_DATA_DIRS") ?? "/usr/local/share:/usr/share").Split(':', StringSplitOptions.RemoveEmptyEntries));

            foreach (var root in roots)
                foreach (var size in IconSizes)
                    foreach (var ext in new[] { ".svg", ".png" })
                    {
                        var p = System.IO.Path.Combine(root, "icons", "hicolor", size, "apps", icon + ext);
                        if (File.Exists(p)) return p;
                    }

            foreach (var ext in new[] { ".png", ".svg", ".xpm" })
            {
                var p = System.IO.Path.Combine("/usr/share/pixmaps", icon + ext);
                if (File.Exists(p)) return p;
            }
            return icon;
        }
    }
}
