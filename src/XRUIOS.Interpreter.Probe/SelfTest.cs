using XRUIOS.Interpreter;
using XRUIOS.Interpreter.Linux;

#pragma warning disable CA1416 // the parsers are pure text functions; the Linux-only attribute is on their classes

namespace XRUIOS.Interpreter.Probe
{
    /// <summary>
    /// "probe selftest": feeds the Linux backend's parsers the output real tools print (pactl in both
    /// JSON and text form, wpctl, amixer, gsettings, .desktop files) plus the shared wire format, so
    /// the Linux side is checked even from a Windows box. Exit code = number of failures.
    /// </summary>
    internal static class SelfTest
    {
        private static int _fail, _pass;

        public static int Run()
        {
            Section("pactl -f json list sink-inputs (pipewire-pulse)");
            var json = LinuxAudio.ParseSinkInputsJson("""
                [{"index":57,"driver":"PipeWire","corked":false,"mute":false,
                  "volume":{"front-left":{"value":42597,"value_percent":"65%","db":"-11.23 dB"},
                            "front-right":{"value":42597,"value_percent":"65%","db":"-11.23 dB"}},
                  "properties":{"application.name":"Firefox","application.process.id":"4242","application.process.binary":"firefox","media.name":"AudioStream"}},
                 {"index":61,"mute":true,"volume":{"mono":{"value":65536,"value_percent":"100%","db":"0.00 dB"}},
                  "properties":{"application.process.binary":"mpv"}}]
                """);
            Eq(json.Count, 2, "two streams");
            Eq(json[0], new AudioChannel("sink-input:57", "Firefox", 65, false), "firefox row");
            Eq(json[1], new AudioChannel("sink-input:61", "mpv", 100, true), "binary used when no app name");

            Section("pactl list sink-inputs (text)");
            var text = LinuxAudio.ParseSinkInputsText(
                "Sink Input #57\n\tDriver: protocol-native.c\n\tOwner Module: 10\n\tClient: 56\n\tSink: 0\n" +
                "\tSample Specification: s16le 2ch 44100Hz\n\tCorked: no\n\tMute: no\n" +
                "\tVolume: front-left: 42597 /  65% / -11.23 dB,   front-right: 42597 /  65% / -11.23 dB\n" +
                "\t        balance 0.00\n\tBuffer Latency: 34512 usec\n\tProperties:\n" +
                "\t\tmedia.name = \"Playback\"\n\t\tapplication.name = \"Spotify\"\n\t\tapplication.process.binary = \"spotify\"\n\n" +
                "Sink Input #58\n\tMute: yes\n\tVolume: mono: 32768 /  50% / -18.06 dB\n\tProperties:\n" +
                "\t\tapplication.process.binary = \"paplay\"\n");
            Eq(text.Count, 2, "two streams");
            Eq(text[0], new AudioChannel("sink-input:57", "Spotify", 65, false), "spotify row");
            Eq(text[1], new AudioChannel("sink-input:58", "paplay", 50, true), "muted mono stream");

            Section("master volume readers");
            Eq(LinuxAudio.FirstPercent("Volume: front-left: 26214 /  40% / -23.88 dB,   front-right: 26214 /  40% / -23.88 dB\n        balance 0.00\n"), 40, "pactl get-sink-volume");
            Eq(LinuxAudio.FirstPercent("Simple mixer control 'Master',0\n  Limits: Playback 0 - 65536\n  Mono:\n  Front Left: Playback 45875 [70%] [on]\n"), 70, "amixer -M get Master");
            Eq(LinuxAudio.WpctlVolume("Volume: 0.45 [MUTED]\n"), (45, true), "wpctl muted");
            Eq(LinuxAudio.WpctlVolume("Volume: 1.00\n"), (100, false), "wpctl full");
            Throws(() => LinuxAudio.FirstPercent("Failed to get sink volume"), "garbage -> error, not 0");

            Section("GNOME input sources");
            Eq(string.Join(",", LinuxKeyboard.ParseSources("[('xkb', 'us'), ('xkb', 'de+nodeadkeys'), ('ibus', 'mozc-jp')]")), "us,de+nodeadkeys", "xkb only, ibus skipped");
            Eq(LinuxKeyboard.ParseSources("@a(ss) []").Count, 0, "empty GVariant array");

            Section(".desktop entries");
            string dir = Path.Combine(Path.GetTempPath(), "xruios-probe-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            try
            {
                string editor = Write(dir, "org.gnome.TextEditor.desktop",
                    "[Desktop Entry]\nType=Application\nName=Text Editor\nName[xx]=Localized\n" +
                    "Exec=gedit %U --new-window \"arg with space\" \"quote\\\"inside\" %i --title=%c 100%%\n" +
                    "Icon=org.gnome.gedit\nTerminal=false\n\n[Desktop Action new-window]\nName=New Window\nExec=gedit --new-window\n");
                var e = LinuxApps.Parse(editor, "org.gnome.TextEditor.desktop");
                Eq(e?.Name, "Text Editor", "unlocalized name (and the action group's Name ignored)");
                Eq(e?.Exec.StartsWith("gedit %U"), true, "main Exec, not the action's");
                Eq(string.Join(" | ", LinuxApps.ExpandExec(e!)),
                    "gedit | --new-window | arg with space | quote\"inside | --icon | org.gnome.gedit | --title=Text Editor | 100%",
                    "field codes + quoting");

                Eq(LinuxApps.Parse(Write(dir, "hidden.desktop", "[Desktop Entry]\nType=Application\nName=X\nExec=x\nNoDisplay=true\n"), "hidden.desktop"), null, "NoDisplay hidden");
                Eq(LinuxApps.Parse(Write(dir, "link.desktop", "[Desktop Entry]\nType=Link\nName=Site\nURL=https://example.com\n"), "link.desktop"), null, "Type=Link skipped");
                Eq(LinuxApps.Parse(Write(dir, "gone.desktop", "[Desktop Entry]\nType=Application\nName=Gone\nExec=gone\nTryExec=/definitely/not/here\n"), "gone.desktop"), null, "missing TryExec skipped");
            }
            finally { Directory.Delete(dir, true); }

            Section("wire format");
            Eq(Wire.Channels(new[] { new AudioChannel("master", "Master", 50, false), new AudioChannel("pid:9", "Evil|App;x\ny", 5, true) }),
                "master|Master|50|0;pid:9|Evil/App,x y|5|1", "separators stripped from names");
            Eq(Wire.Battery(new BatteryStatus(true, 81, false, false, TimeSpan.FromMinutes(95))), "81|0|0|95", "on battery");
            Eq(Wire.Battery(new BatteryStatus(false, -1, false, true, null)), "-1|0|1|-1", "desktop");
            Eq(Wire.ParsePower("lock"), PowerAction.Lock, "case-insensitive action");
            Throws(() => Wire.ParsePower("7"), "numeric action rejected");

            Section("per-area overrides");
            var platformNotes = XruiosInterpreter.Platform.Notifications;
            var fakeNotes = new FakeNotifications();
            XruiosInterpreter.Use<INotificationInterpreter>(fakeNotes);
            try
            {
                Eq(ReferenceEquals(XruiosInterpreter.Current.Notifications, fakeNotes), true, "override replaces one area");
                Eq(ReferenceEquals(XruiosInterpreter.Current.Audio, XruiosInterpreter.Platform.Audio), true, "other areas stay on the platform");
            }
            finally { XruiosInterpreter.Use<INotificationInterpreter>(null); }
            Eq(ReferenceEquals(XruiosInterpreter.Current.Notifications, platformNotes), true, "removing it restores the platform");
            Eq(XruiosInterpreter.Get<IThemeInterpreter>(), null, "developer-owned area has no default");

            Section("event dispatch to developer interpreters");
            var themes = new FakeThemes();
            string? unhandled = null;
            Action<string, string> spy = (n, _) => unhandled = n;
            InterpreterEvents.Unhandled += spy;
            try
            {
                var info = new ThemeInfo("solitude", "Solitude", "#FFFFFF", "#6B6661", null);
                Eq(InterpreterEvents.Dispatch(XruiosEventNames.ThemeChanged, EventJson.Write(info)), false, "no interpreter registered -> not handled");
                Eq(unhandled, XruiosEventNames.ThemeChanged, "...and surfaced as Unhandled");

                XruiosInterpreter.Use<IThemeInterpreter>(themes);
                Eq(InterpreterEvents.Dispatch(XruiosEventNames.ThemeChanged, EventJson.Write(info)), true, "registered -> handled");
                Eq(themes.Last, info, "payload round-trips intact");

                Eq(InterpreterEvents.Dispatch(XruiosEventNames.NotificationPosted,
                    EventJson.Write(new NotificationInfo("n1", "Mail", "Hi", "Body", DateTimeOffset.Now))), false,
                    "notifications need an explicit presenter (worker already shows the OS toast)");
            }
            finally
            {
                XruiosInterpreter.Use<IThemeInterpreter>(null);
                InterpreterEvents.Unhandled -= spy;
            }
            Eq(DesktopThemeInterpreter.Luminance("#6B6661") < 0.5, true, "taupe reads as dark");
            Eq(DesktopThemeInterpreter.Luminance("#FFF") > 0.9, true, "#FFF shorthand reads as light");
            Eq(DesktopThemeInterpreter.Luminance("not a colour"), null, "garbage colour -> no mode change");

            Console.WriteLine($"\n{_pass} passed, {_fail} failed");
            return _fail;
        }

        private static string Write(string dir, string name, string content)
        {
            string p = Path.Combine(dir, name);
            File.WriteAllText(p, content);
            return p;
        }

        private sealed class FakeNotifications : INotificationInterpreter
        {
            public void Show(string title, string body, string? source = null) { }
        }

        private sealed class FakeThemes : IThemeInterpreter
        {
            public ThemeInfo? Last { get; private set; }
            public void Apply(ThemeInfo theme) => Last = theme;
        }

        private static void Section(string name) => Console.WriteLine($"\n[{name}]");

        private static void Eq<T>(T actual, T expected, string what)
        {
            bool ok = EqualityComparer<T>.Default.Equals(actual, expected);
            if (ok) _pass++; else _fail++;
            Console.WriteLine(ok ? $"  PASS {what}" : $"  FAIL {what}\n       expected: {expected}\n       actual:   {actual}");
        }

        private static void Throws(Action a, string what)
        {
            try { a(); _fail++; Console.WriteLine($"  FAIL {what} (no exception)"); }
            catch { _pass++; Console.WriteLine($"  PASS {what}"); }
        }
    }
}
