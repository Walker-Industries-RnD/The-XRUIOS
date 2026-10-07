using EclipseProject;
using XRUIOS.Interfaces;
using XRUIOS.Interpreter;
using static XRUIOS.Barebones.Interfaces.ThemeSystem;
using Themes = XRUIOS.Barebones.Functions.ThemeSystem;

namespace XRUIOS.Worker.Theme
{
    // Themes are saved as Themes/<file>.json (assets in Themes/<file>/). ApplyTheme makes one current and
    // announces ThemeChanged - what a theme MEANS is the host's call (an IThemeInterpreter: reskin VR
    // panels, recolour a desktop shell...). Mirroring it onto the OS desktop is one such interpreter
    // (DesktopThemeInterpreter), opt-in via SetDesktopSync. ListThemes/ApplyTheme/GetActiveTheme are
    // what the ThemePicker hole calls.
    public static class ThemeCapabilities
    {
        private static string ThemesDir => Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "Themes");
        private static string StateDir => Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "ThemeState");
        private static string ActiveFile => Path.Combine(StateDir, "active");
        private static string DesktopSyncFile => Path.Combine(StateDir, "desktop-sync");

        [SeaOfDirac("Theme.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Theme: " + input;

        /// <summary>"id|name|swatch;..." - id is the theme's file name, swatch its primary accent colour.</summary>
        [SeaOfDirac("ListThemes", new string[0], typeof(string))]
        public static async Task<string> ListThemes()
        {
            if (!Directory.Exists(ThemesDir)) return "";
            var rows = new List<string>();
            foreach (var file in Directory.EnumerateFiles(ThemesDir, "*.json"))
            {
                string id = Path.GetFileNameWithoutExtension(file);
                try { rows.Add(Row(Info(id, await Themes.GetXRUIOSTheme(id)))); }
                catch { /* unreadable / other user's key: leave it out of the picker */ }
            }
            return string.Join(';', rows);
        }

        /// <summary>Returns "applied &lt;name&gt;", plus what desktop sync did when it's on.</summary>
        [SeaOfDirac("ApplyTheme", new[] { "themeId" }, typeof(string), typeof(string))]
        public static async Task<string> ApplyTheme(string themeId)
        {
            if (themeId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || themeId.Contains(".."))
                throw new ArgumentException($"'{themeId}' is not a theme id.");
            if (!File.Exists(Path.Combine(ThemesDir, themeId + ".json")))
                throw new KeyNotFoundException($"No theme '{themeId}'.");

            await Themes.SetTheme(themeId);
            var info = Info(themeId, Themes.CurrentTheme);
            Directory.CreateDirectory(StateDir);
            await File.WriteAllTextAsync(ActiveFile, themeId);

            WorkerEvents.Publish(XruiosEventNames.ThemeChanged, EventJson.Write(info));

            if (!DesktopSyncEnabled()) return $"applied {info.Name}";
            var desktop = new DesktopThemeInterpreter();
            desktop.Apply(info);
            return desktop.LastResult.Length == 0 ? $"applied {info.Name}" : $"applied {info.Name}; desktop: {Wire.Field(desktop.LastResult)}";
        }

        /// <summary>"id|name|swatch" of the current theme, or "" if none has been applied.</summary>
        [SeaOfDirac("GetActiveTheme", new string[0], typeof(string))]
        public static async Task<string> GetActiveTheme()
        {
            if (!File.Exists(ActiveFile)) return "";
            string id = (await File.ReadAllTextAsync(ActiveFile)).Trim();
            if (id.Length == 0 || !File.Exists(Path.Combine(ThemesDir, id + ".json"))) return "";
            return Row(Info(id, await Themes.GetXRUIOSTheme(id)));
        }

        /// <summary>Mirror applied themes onto the OS desktop (dark mode + wallpaper). Off by default.</summary>
        [SeaOfDirac("SetDesktopSync", new[] { "enabled" }, typeof(string), typeof(bool))]
        public static async Task<string> SetDesktopSync(bool enabled)
        {
            Directory.CreateDirectory(StateDir);
            await File.WriteAllTextAsync(DesktopSyncFile, enabled ? "on" : "off");
            return enabled ? "on" : "off";
        }

        private static bool DesktopSyncEnabled() =>
            File.Exists(DesktopSyncFile) && File.ReadAllText(DesktopSyncFile).Trim() == "on";

        /// <summary>The host-facing view of a theme, with its background image resolved to a real path.</summary>
        private static ThemeInfo Info(string id, XRUIOSTheme t) => new(
            id,
            t.Identity?.Name ?? id,
            t.Colors?.AccentPrimary.Item2,
            t.Colors?.BackgroundPrimary.Item2,
            ResolveImage(id, t.Colors?.BackgroundPrimary.Item1));

        private static string Row(ThemeInfo t) => $"{Wire.Field(t.Id)}|{Wire.Field(t.Name)}|{Wire.Field(t.AccentColor)}";

        /// <summary>A theme image path is absolute or relative to the theme's asset folder.</summary>
        private static string? ResolveImage(string themeId, string? image)
        {
            if (string.IsNullOrWhiteSpace(image)) return null;
            string path = Path.IsPathRooted(image) ? image : Path.Combine(ThemesDir, themeId, image);
            return File.Exists(path) ? path : null;
        }
    }
}
