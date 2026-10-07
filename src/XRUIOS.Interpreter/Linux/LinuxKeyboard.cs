using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace XRUIOS.Interpreter.Linux
{
    /// <summary>
    /// Layout ids use GNOME's xkb spelling: "us", "de+nodeadkeys". Backends: GNOME input sources
    /// (gsettings) when the user has any configured, else the X server's xkb map (setxkbmap). With
    /// nothing configured the list falls back to every layout xkb knows, so a picker is never empty.
    /// </summary>
    [SupportedOSPlatform("linux")]
    internal sealed partial class LinuxKeyboard : IKeyboardInterpreter
    {
        private const string GnomeSchema = "org.gnome.desktop.input-sources";

        public IReadOnlyList<KeyboardLayout> ListLayouts()
        {
            var ids = GnomeSources() ?? XkbQuery();
            if (ids is null || ids.Count == 0) ids = XkbCatalog.Value.Layouts.Keys.ToList();
            return ids.Select(id => new KeyboardLayout(id, DisplayName(id))).ToList();
        }

        public string? GetActiveLayout()
        {
            var gnome = GnomeSources();
            if (gnome is { Count: > 0 })
            {
                // mru-sources[0] is the one in use right now; sources[0] is the fallback.
                var mru = ParseSources(Gsettings("get", GnomeSchema, "mru-sources"));
                return mru.FirstOrDefault(gnome.Contains) ?? gnome[0];
            }
            return XkbQuery()?.FirstOrDefault();
        }

        public void ApplyLayout(string layoutId)
        {
            var gnome = GnomeSources();
            if (gnome is { Count: > 0 })
            {
                // Moving the source to the front makes GNOME Shell switch to it.
                var reordered = new List<string> { layoutId };
                reordered.AddRange(gnome.Where(x => x != layoutId));
                string value = "[" + string.Join(", ", reordered.Select(id => $"('xkb', '{Safe(id)}')")) + "]";
                Gsettings("set", GnomeSchema, "sources", value);
                return;
            }

            if (Shell.Has("setxkbmap") && Environment.GetEnvironmentVariable("DISPLAY") is { Length: > 0 })
            {
                var current = XkbQuery() ?? new List<string>();
                var order = new List<string> { layoutId };
                order.AddRange(current.Where(x => x != layoutId));
                string layouts = string.Join(",", order.Select(id => Split(id).layout));
                string variants = string.Join(",", order.Select(id => Split(id).variant));
                Shell.RunChecked("setxkbmap", new[] { "-layout", layouts, "-variant", variants });
                return;
            }

            throw new PlatformNotSupportedException("Can't switch keyboard layouts here: no GNOME input sources and no X11 setxkbmap.");
        }

        // ── GNOME ────────────────────────────────────────────────────────────────────────────────

        private static List<string>? GnomeSources()
        {
            if (!Shell.Has("gsettings")) return null;
            var r = Shell.Run("gsettings", new[] { "get", GnomeSchema, "sources" });
            if (!r.Ok) return null;
            var list = ParseSources(r.StdOut);
            return list.Count == 0 ? null : list;
        }

        [GeneratedRegex(@"\('xkb',\s*'([^']+)'\)")] private static partial Regex XkbSourceRx();

        /// <summary>"[('xkb', 'us'), ('xkb', 'de+nodeadkeys'), ('ibus', 'mozc-jp')]" → [us, de+nodeadkeys]</summary>
        internal static List<string> ParseSources(string gvariant)
            => XkbSourceRx().Matches(gvariant).Select(m => m.Groups[1].Value).ToList();

        private static string Gsettings(params string[] args) => Shell.RunChecked("gsettings", args);

        // Layout ids end up inside a GVariant string literal; allow only xkb's own id alphabet.
        [GeneratedRegex(@"^[A-Za-z0-9_+\-()]+$")] private static partial Regex LayoutIdRx();

        private static string Safe(string id) =>
            LayoutIdRx().IsMatch(id) ? id : throw new ArgumentException($"'{id}' is not a valid keyboard layout id.");

        // ── X11 ──────────────────────────────────────────────────────────────────────────────────

        private static List<string>? XkbQuery()
        {
            if (!Shell.Has("setxkbmap") || Environment.GetEnvironmentVariable("DISPLAY") is not { Length: > 0 }) return null;
            var r = Shell.Run("setxkbmap", new[] { "-query" }, cLocale: true);
            if (!r.Ok) return null;

            string[] layouts = Array.Empty<string>(), variants = Array.Empty<string>();
            foreach (var line in r.StdOut.Split('\n'))
            {
                if (line.StartsWith("layout:")) layouts = line[7..].Trim().Split(',');
                else if (line.StartsWith("variant:")) variants = line[8..].Trim().Split(',');
            }
            return layouts.Select((l, i) => i < variants.Length && variants[i].Length > 0 ? $"{l}+{variants[i]}" : l).ToList();
        }

        private static (string layout, string variant) Split(string id)
        {
            Safe(id);
            int plus = id.IndexOf('+');
            return plus < 0 ? (id, "") : (id[..plus], id[(plus + 1)..]);
        }

        // ── human names from xkb's catalogue ─────────────────────────────────────────────────────

        private sealed record Catalog(Dictionary<string, string> Layouts, Dictionary<string, string> Variants);

        private static readonly Lazy<Catalog> XkbCatalog = new(LoadCatalog);

        private static string DisplayName(string id)
        {
            var (layout, variant) = Split(id);
            var cat = XkbCatalog.Value;
            if (variant.Length > 0 && cat.Variants.TryGetValue($"{layout}+{variant}", out var v)) return v;
            return cat.Layouts.TryGetValue(layout, out var l) ? l : id;
        }

        /// <summary>
        /// /usr/share/X11/xkb/rules/evdev.lst: "! layout" lines are "  us   English (US)";
        /// "! variant" lines are "  intl   us: English (US, intl., with dead keys)".
        /// </summary>
        private static Catalog LoadCatalog()
        {
            var layouts = new Dictionary<string, string>(StringComparer.Ordinal);
            var variants = new Dictionary<string, string>(StringComparer.Ordinal);
            string path = "/usr/share/X11/xkb/rules/evdev.lst";
            if (!File.Exists(path)) return new Catalog(layouts, variants);

            string section = "";
            foreach (var raw in File.ReadLines(path))
            {
                if (raw.StartsWith('!')) { section = raw[1..].Trim(); continue; }
                var line = raw.Trim();
                if (line.Length == 0) continue;
                int sp = line.IndexOfAny(new[] { ' ', '\t' });
                if (sp < 0) continue;
                string key = line[..sp];
                string rest = line[sp..].Trim();

                if (section == "layout") layouts[key] = rest;
                else if (section == "variant")
                {
                    int colon = rest.IndexOf(':');
                    if (colon > 0) variants[$"{rest[..colon]}+{key}"] = rest[(colon + 1)..].Trim();
                }
            }
            return new Catalog(layouts, variants);
        }
    }
}
