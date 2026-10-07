using System.Globalization;
using System.Runtime.Versioning;

namespace XRUIOS.Interpreter.Linux
{
    /// <summary>
    /// Linux backend. Talks to the standard desktop plumbing - systemd/logind, PulseAudio/PipeWire,
    /// freedesktop notifications, wl-clipboard/xclip, gsettings/Plasma - so it works across distros
    /// and both Wayland and X11. The worker must run inside the user's session (the Manager launched
    /// from the login session inherits DISPLAY / WAYLAND_DISPLAY / DBUS_SESSION_BUS_ADDRESS).
    /// </summary>
    [SupportedOSPlatform("linux")]
    public sealed class LinuxInterpreter : IPlatformInterpreter
    {
        public InterpreterPlatform Platform => InterpreterPlatform.Linux;
        public IAudioInterpreter Audio { get; } = new LinuxAudio();
        public IPowerInterpreter Power { get; } = new LinuxPower();
        public IClipboardInterpreter Clipboard { get; } = new LinuxClipboard();
        public IAppInterpreter Apps { get; } = new LinuxApps();
        public INotificationInterpreter Notifications { get; } = new LinuxNotifications();
        public IKeyboardInterpreter Keyboard { get; } = new LinuxKeyboard();
        public IAppearanceInterpreter Appearance { get; } = new LinuxAppearance();
        public ISystemInfoInterpreter SystemInfo { get; } = new LinuxSystemInfo();

        internal static bool IsWayland => Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") is { Length: > 0 };
        internal static bool IsX11 => Environment.GetEnvironmentVariable("DISPLAY") is { Length: > 0 };

        internal static string Desktop =>
            (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? Environment.GetEnvironmentVariable("DESKTOP_SESSION") ?? "").ToUpperInvariant();

        internal static bool IsKde => Desktop.Contains("KDE");
        internal static bool IsGnomeLike =>
            Desktop.Contains("GNOME") || Desktop.Contains("UNITY") || Desktop.Contains("BUDGIE") || Desktop.Contains("PANTHEON");
    }

    // ── Power ────────────────────────────────────────────────────────────────────────────────────

    [SupportedOSPlatform("linux")]
    internal sealed class LinuxPower : IPowerInterpreter
    {
        public void Request(PowerAction action)
        {
            string? session = Environment.GetEnvironmentVariable("XDG_SESSION_ID");
            switch (action)
            {
                case PowerAction.Shutdown: Login("poweroff"); break;
                case PowerAction.Restart: Login("reboot"); break;
                case PowerAction.Sleep: Login("suspend"); break;
                case PowerAction.Hibernate: Login("hibernate"); break;

                case PowerAction.Lock:
                    if (Shell.Has("loginctl"))
                        Shell.RunChecked("loginctl", session is { Length: > 0 } ? new[] { "lock-session", session } : new[] { "lock-session" });
                    else if (Shell.Has("xdg-screensaver")) Shell.RunChecked("xdg-screensaver", new[] { "lock" });
                    else throw new PlatformNotSupportedException("No way to lock the session (need loginctl or xdg-screensaver).");
                    break;

                case PowerAction.SignOut:
                    if (session is { Length: > 0 } && Shell.Has("loginctl"))
                        Shell.RunChecked("loginctl", new[] { "terminate-session", session });
                    else if (Shell.Has("gnome-session-quit"))
                        Shell.RunChecked("gnome-session-quit", new[] { "--logout", "--no-prompt" });
                    else if (Shell.Has("qdbus"))
                        Shell.RunChecked("qdbus", new[] { "org.kde.Shutdown", "/Shutdown", "logout" });
                    else throw new PlatformNotSupportedException("No way to sign out (need XDG_SESSION_ID + loginctl, gnome-session-quit or qdbus).");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        /// <summary>systemd's systemctl, else elogind's loginctl (same verbs, polkit-authorized for the active seat).</summary>
        private static void Login(string verb)
        {
            if (Shell.Has("systemctl")) Shell.RunChecked("systemctl", new[] { verb }, timeoutMs: 15000);
            else if (Shell.Has("loginctl")) Shell.RunChecked("loginctl", new[] { verb }, timeoutMs: 15000);
            else throw new PlatformNotSupportedException($"Can't {verb}: neither systemctl nor loginctl is installed.");
        }

        public BatteryStatus GetBattery()
        {
            const string root = "/sys/class/power_supply";
            if (!Directory.Exists(root)) return new BatteryStatus(false, -1, false, true, null);

            bool pluggedIn = false;
            var batteries = new List<(int pct, string status, double? hours)>();

            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                string type = Read(dir, "type") ?? "";
                if (type == "Mains" || type == "USB")
                {
                    if (Read(dir, "online") == "1") pluggedIn = true;
                }
                else if (type == "Battery" && Read(dir, "scope") != "Device")   // skip mice / controllers
                {
                    int pct = int.TryParse(Read(dir, "capacity"), out var c) ? c : -1;
                    string status = Read(dir, "status") ?? "Unknown";
                    batteries.Add((pct, status, HoursLeft(dir, status)));
                }
            }

            if (batteries.Count == 0) return new BatteryStatus(false, -1, false, true, null);

            var known = batteries.Where(b => b.pct >= 0).ToList();
            int percent = known.Count == 0 ? -1 : (int)Math.Round(known.Average(b => b.pct));
            bool charging = batteries.Any(b => b.status == "Charging");
            if (charging || batteries.All(b => b.status is "Full" or "Not charging")) pluggedIn = true;

            double? hours = batteries.All(b => b.hours.HasValue) ? batteries.Sum(b => b.hours!.Value) : null;
            return new BatteryStatus(true, percent, charging, pluggedIn,
                !charging && hours is > 0 ? TimeSpan.FromHours(hours.Value) : null);
        }

        /// <summary>energy_now/power_now (µWh/µW) or charge_now/current_now (µAh/µA) while discharging.</summary>
        private static double? HoursLeft(string dir, string status)
        {
            if (status != "Discharging") return null;
            double? now = Num(dir, "energy_now") ?? Num(dir, "charge_now");
            double? rate = Num(dir, "power_now") ?? Num(dir, "current_now");
            return now is > 0 && rate is > 0 ? now / rate : null;
        }

        private static string? Read(string dir, string file)
        {
            try { var p = Path.Combine(dir, file); return File.Exists(p) ? File.ReadAllText(p).Trim() : null; }
            catch { return null; }
        }

        private static double? Num(string dir, string file) =>
            double.TryParse(Read(dir, file), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    // ── Clipboard ────────────────────────────────────────────────────────────────────────────────

    [SupportedOSPlatform("linux")]
    internal sealed class LinuxClipboard : IClipboardInterpreter
    {
        public string? GetText()
        {
            Shell.Result r;
            if (LinuxInterpreter.IsWayland && Shell.Has("wl-paste")) r = Shell.Run("wl-paste", new[] { "--no-newline", "--type", "text" });
            else if (LinuxInterpreter.IsX11 && Shell.Has("xclip")) r = Shell.Run("xclip", new[] { "-selection", "clipboard", "-o" });
            else if (LinuxInterpreter.IsX11 && Shell.Has("xsel")) r = Shell.Run("xsel", new[] { "--clipboard", "--output" });
            else throw NoTool();

            // All three exit non-zero when the clipboard is empty or holds no text.
            return r.Ok && r.StdOut.Length > 0 ? r.StdOut : null;
        }

        public void SetText(string text)
        {
            // These tools fork a daemon that serves the selection, so don't wait on their pipes.
            if (LinuxInterpreter.IsWayland && Shell.Has("wl-copy")) Shell.RunDetached("wl-copy", Array.Empty<string>(), text);
            else if (LinuxInterpreter.IsX11 && Shell.Has("xclip")) Shell.RunDetached("xclip", new[] { "-selection", "clipboard", "-i" }, text);
            else if (LinuxInterpreter.IsX11 && Shell.Has("xsel")) Shell.RunDetached("xsel", new[] { "--clipboard", "--input" }, text);
            else throw NoTool();
        }

        private static Exception NoTool() => new PlatformNotSupportedException(
            LinuxInterpreter.IsWayland ? "Clipboard needs wl-clipboard (wl-copy / wl-paste)."
            : LinuxInterpreter.IsX11 ? "Clipboard needs xclip or xsel."
            : "No graphical session (neither WAYLAND_DISPLAY nor DISPLAY is set), so there is no clipboard.");
    }

    // ── Notifications ────────────────────────────────────────────────────────────────────────────

    [SupportedOSPlatform("linux")]
    internal sealed class LinuxNotifications : INotificationInterpreter
    {
        public void Show(string title, string body, string? source = null)
        {
            string app = string.IsNullOrWhiteSpace(source) ? "XRUIOS" : source;
            if (Shell.Has("notify-send"))
            {
                Shell.RunChecked("notify-send", new[] { "--app-name", app, "--", title, body });
                return;
            }
            if (Shell.Has("gdbus"))
            {
                // org.freedesktop.Notifications.Notify(app, replaces_id, icon, summary, body, actions, hints, timeout)
                Shell.RunChecked("gdbus", new[]
                {
                    "call", "--session", "--dest", "org.freedesktop.Notifications",
                    "--object-path", "/org/freedesktop/Notifications",
                    "--method", "org.freedesktop.Notifications.Notify",
                    GVariantString(app), "0", "''", GVariantString(title), GVariantString(body), "[]", "{}", "5000",
                });
                return;
            }
            throw new PlatformNotSupportedException("Notifications need notify-send (libnotify-bin) or gdbus.");
        }

        /// <summary>gdbus parses its arguments as GVariant text, so quote/escape strings ourselves.</summary>
        private static string GVariantString(string s) => "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
    }

    // ── Appearance ───────────────────────────────────────────────────────────────────────────────

    [SupportedOSPlatform("linux")]
    internal sealed class LinuxAppearance : IAppearanceInterpreter
    {
        public bool? GetDarkMode()
        {
            if (LinuxInterpreter.IsKde)
            {
                string? tool = Shell.Has("kreadconfig6") ? "kreadconfig6" : Shell.Has("kreadconfig5") ? "kreadconfig5" : null;
                if (tool is null) return null;
                var r = Shell.Run(tool, new[] { "--file", "kdeglobals", "--group", "General", "--key", "ColorScheme" });
                return r.Ok ? r.StdOut.Contains("Dark", StringComparison.OrdinalIgnoreCase) : null;
            }
            if (Shell.Has("gsettings"))
            {
                var r = Shell.Run("gsettings", new[] { "get", "org.gnome.desktop.interface", "color-scheme" });
                if (r.Ok) return r.StdOut.Contains("prefer-dark");
            }
            return null;
        }

        public void SetDarkMode(bool dark)
        {
            if (LinuxInterpreter.IsKde && Shell.Has("plasma-apply-colorscheme"))
            {
                Shell.RunChecked("plasma-apply-colorscheme", new[] { dark ? "BreezeDark" : "BreezeLight" });
                return;
            }
            if (Shell.Has("gsettings"))
            {
                // GNOME 42+ (and the xdg-desktop-portal setting most GTK4/libadwaita/Qt6 apps follow).
                Shell.RunChecked("gsettings", new[] { "set", "org.gnome.desktop.interface", "color-scheme", dark ? "prefer-dark" : "default" });
                return;
            }
            throw new PlatformNotSupportedException($"Don't know how to switch dark mode on '{LinuxInterpreter.Desktop}'.");
        }

        public void SetWallpaper(string imagePath)
        {
            string full = Path.GetFullPath(imagePath);
            if (!File.Exists(full)) throw new FileNotFoundException("Wallpaper image not found.", full);
            string uri = new Uri(full).AbsoluteUri;

            if (LinuxInterpreter.IsKde && Shell.Has("plasma-apply-wallpaperimage"))
            {
                Shell.RunChecked("plasma-apply-wallpaperimage", new[] { full });
            }
            else if (LinuxInterpreter.Desktop.Contains("CINNAMON") && Shell.Has("gsettings"))
            {
                Shell.RunChecked("gsettings", new[] { "set", "org.cinnamon.desktop.background", "picture-uri", uri });
            }
            else if (LinuxInterpreter.IsGnomeLike && Shell.Has("gsettings"))
            {
                Shell.RunChecked("gsettings", new[] { "set", "org.gnome.desktop.background", "picture-uri", uri });
                // GNOME 42+ keeps a separate dark-style wallpaper; older versions don't have the key.
                Shell.Run("gsettings", new[] { "set", "org.gnome.desktop.background", "picture-uri-dark", uri });
            }
            else if (LinuxInterpreter.IsX11 && Shell.Has("feh"))
            {
                Shell.RunChecked("feh", new[] { "--bg-fill", full });
            }
            else throw new PlatformNotSupportedException($"Don't know how to set the wallpaper on '{LinuxInterpreter.Desktop}'.");
        }
    }
}
