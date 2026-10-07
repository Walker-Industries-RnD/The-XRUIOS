using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;

namespace XRUIOS.Interpreter.Windows
{
    /// <summary>
    /// "Installed apps" = the Start Menu, exactly as the user sees it: every .lnk / .url / .appref-ms
    /// under the per-user and all-users Programs folders. The id is the shortcut's path relative to
    /// its Programs root (so it's stable and readable); launching goes through the shell so the
    /// shortcut's own arguments, working directory and elevation manifest are honoured.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class WindowsApps : IAppInterpreter
    {
        private static readonly string[] ShortcutExts = { ".lnk", ".url", ".appref-ms" };
        private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

        private IReadOnlyList<InstalledApp>? _cache;
        private DateTime _cachedAt;
        private readonly object _gate = new();

        public IReadOnlyList<InstalledApp> ListInstalled()
        {
            lock (_gate)
            {
                if (_cache != null && DateTime.UtcNow - _cachedAt < CacheFor) return _cache;

                var byId = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);
                foreach (var root in ProgramRoots())
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (var file in SafeEnumerate(root))
                    {
                        if (!ShortcutExts.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) continue;

                        string id = Path.ChangeExtension(Path.GetRelativePath(root, file), null).Replace('\\', '/');
                        if (byId.ContainsKey(id)) continue;   // per-user shortcut wins over the all-users copy
                        if (LooksLikeUninstaller(id)) continue;

                        byId[id] = new InstalledApp(id, Path.GetFileNameWithoutExtension(file), IconFor(file), file);
                    }
                }

                _cache = byId.Values.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                _cachedAt = DateTime.UtcNow;
                return _cache;
            }
        }

        public int Launch(string appId)
        {
            var app = ListInstalled().FirstOrDefault(a => string.Equals(a.Id, appId, StringComparison.OrdinalIgnoreCase))
                      ?? throw new KeyNotFoundException($"No installed app with id '{appId}'.");
            return ShellStart(app.Target);
        }

        public int LaunchRegistered(string entryPoint)
        {
            if (!File.Exists(entryPoint)) throw new FileNotFoundException("Registered entry point not found.", entryPoint);
            return ShellStart(entryPoint);
        }

        private static int ShellStart(string target)
        {
            using var p = Process.Start(new ProcessStartInfo(target)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(target) ?? "",
            });
            try { return p?.Id ?? 0; } catch { return 0; }
        }

        private static IEnumerable<string> ProgramRoots() => new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
        };

        private static IEnumerable<string> SafeEnumerate(string root)
        {
            try
            {
                return Directory.EnumerateFiles(root, "*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.System,
                });
            }
            catch { return Array.Empty<string>(); }
        }

        private static bool LooksLikeUninstaller(string id)
        {
            var name = Path.GetFileName(id);
            return name.StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase)
                || name.Contains("uninstall", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Icon source for a shortcut: its explicit IconLocation, else its target, else itself.</summary>
        private static string IconFor(string shortcut)
        {
            if (!shortcut.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return shortcut;
            object? link = null;
            try
            {
                link = new ShellLinkComObject();
                ((IPersistFile)link).Load(shortcut, 0 /* STGM_READ */);
                var shell = (IShellLinkW)link;

                var sb = new StringBuilder(1024);
                shell.GetIconLocation(sb, sb.Capacity, out int iconIndex);
                string icon = Environment.ExpandEnvironmentVariables(sb.ToString());
                if (icon.Length > 0 && File.Exists(icon)) return iconIndex == 0 ? icon : $"{icon},{iconIndex}";

                sb.Clear();
                shell.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
                string target = sb.ToString();
                if (target.Length > 0 && File.Exists(target)) return target;
            }
            catch { /* unreadable / broken shortcut: the shortcut itself still carries an icon */ }
            finally
            {
                if (link != null) Marshal.ReleaseComObject(link);
            }
            return shortcut;
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLinkComObject { }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int cch, IntPtr findData, uint flags);
            void GetIDList(out IntPtr pidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
            void GetHotkey(out short hotkey);
            void SetHotkey(short hotkey);
            void GetShowCmd(out int showCmd);
            void SetShowCmd(int showCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int cch, out int iconIndex);
        }
    }
}
