using System.Globalization;

namespace XRUIOS.Interpreter
{
    /// <summary>
    /// The name → action table: a caller names a function ("audio.master.set") with string
    /// arguments, and the interpreter runs it on whatever backend is current. The Manager path goes
    /// through the workers' [SeaOfDirac] capabilities instead; this table is for in-process hosts (the
    /// Aether Engine's bus, the probe CLI, tests) so every caller speaks the same verbs and wire format.
    /// </summary>
    public static class InterpreterCommands
    {
        private delegate string Handler(IPlatformInterpreter os, IReadOnlyDictionary<string, string> a);

        private static readonly Dictionary<string, (string help, Handler run)> Table = new(StringComparer.OrdinalIgnoreCase)
        {
            ["platform"] = ("which backend is active", (os, _) => os.Platform.ToString()),

            ["audio.list"] = ("master + per-app channels", (os, _) => Wire.Channels(os.Audio.ListChannels())),
            ["audio.master.get"] = ("master volume 0-100", (os, _) => os.Audio.GetMasterVolume().ToString(CultureInfo.InvariantCulture)),
            ["audio.master.set"] = ("volume=0-100", (os, a) => { os.Audio.SetMasterVolume(Int(a, "volume")); return "ok"; }),
            ["audio.master.mute"] = ("muted=true|false", (os, a) => { os.Audio.SetMasterMute(Bool(a, "muted")); return "ok"; }),
            ["audio.channel.set"] = ("id=<channel> volume=0-100", (os, a) => { os.Audio.SetChannelVolume(Req(a, "id"), Int(a, "volume")); return "ok"; }),
            ["audio.channel.mute"] = ("id=<channel> muted=true|false", (os, a) => { os.Audio.SetChannelMute(Req(a, "id"), Bool(a, "muted")); return "ok"; }),
            ["audio.channel.toggle"] = ("id=<channel>", (os, a) => Wire.ToggleMute(os.Audio, Req(a, "id"))),

            ["power.battery"] = ("percent|charging|pluggedIn|minutesLeft", (os, _) => Wire.Battery(os.Power.GetBattery())),
            ["power.request"] = ("action=Shutdown|Restart|Sleep|Hibernate|SignOut|Lock", (os, a) => { os.Power.Request(Wire.ParsePower(Req(a, "action"))); return "ok"; }),

            ["clipboard.get"] = ("OS clipboard text", (os, _) => os.Clipboard.GetText() ?? ""),
            ["clipboard.set"] = ("text=...", (os, a) => { os.Clipboard.SetText(Req(a, "text")); return "ok"; }),

            ["apps.list"] = ("installed apps id|name|icon", (os, _) => Wire.Apps(os.Apps.ListInstalled())),
            ["apps.launch"] = ("id=<app id>", (os, a) => os.Apps.Launch(Req(a, "id")).ToString(CultureInfo.InvariantCulture)),

            ["notify"] = ("title=... body=... [source=...]", (os, a) => { os.Notifications.Show(Req(a, "title"), a.GetValueOrDefault("body") ?? "", a.GetValueOrDefault("source")); return "ok"; }),

            ["keyboard.list"] = ("installed layouts id|name", (os, _) => Wire.Layouts(os.Keyboard.ListLayouts())),
            ["keyboard.active"] = ("active layout id", (os, _) => os.Keyboard.GetActiveLayout() ?? ""),
            ["keyboard.apply"] = ("id=<layout id>", (os, a) => { os.Keyboard.ApplyLayout(Req(a, "id")); return "ok"; }),

            ["appearance.dark.get"] = ("dark|light|unknown", (os, _) => os.Appearance.GetDarkMode() switch { true => "dark", false => "light", null => "unknown" }),
            ["appearance.dark.set"] = ("dark=true|false", (os, a) => { os.Appearance.SetDarkMode(Bool(a, "dark")); return "ok"; }),
            ["appearance.wallpaper"] = ("path=<image>", (os, a) => { os.Appearance.SetWallpaper(Req(a, "path")); return "ok"; }),

            ["system.specs"] = ("machine specs", (os, _) => Wire.Specs(os.SystemInfo.GetSpecs())),
        };

        public static IEnumerable<(string name, string help)> List() => Table.Select(kv => (kv.Key, kv.Value.help));

        public static bool Exists(string name) => Table.ContainsKey(name);

        /// <summary>Run a command against <see cref="XruiosInterpreter.Current"/> (or the backend given).</summary>
        public static string Execute(string name, IReadOnlyDictionary<string, string>? args = null, IPlatformInterpreter? os = null)
        {
            if (!Table.TryGetValue(name, out var entry))
                throw new KeyNotFoundException($"Unknown interpreter command '{name}'.");
            return entry.run(os ?? XruiosInterpreter.Current, args ?? new Dictionary<string, string>());
        }

        private static string Req(IReadOnlyDictionary<string, string> a, string key) =>
            a.TryGetValue(key, out var v) && v.Length > 0 ? v : throw new ArgumentException($"Missing argument '{key}'.");

        private static int Int(IReadOnlyDictionary<string, string> a, string key) =>
            int.TryParse(Req(a, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : throw new ArgumentException($"'{key}' must be a whole number.");

        private static bool Bool(IReadOnlyDictionary<string, string> a, string key) => Req(a, key).ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "on" => true,
            "false" or "0" or "no" or "off" => false,
            _ => throw new ArgumentException($"'{key}' must be true or false."),
        };
    }

    /// <summary>
    /// The string wire format the XRUIOS holes parse: rows joined by ';', fields by '|'. Shared by the
    /// worker capabilities and <see cref="InterpreterCommands"/> so they can never drift apart.
    /// </summary>
    public static class Wire
    {
        /// <summary>Strip the row/field separators (and newlines) out of free text.</summary>
        public static string Field(string? s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace('|', '/').Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ');

        public static string Channels(IEnumerable<AudioChannel> rows) =>
            string.Join(';', rows.Select(c => $"{Field(c.Id)}|{Field(c.Name)}|{c.Volume}|{(c.Muted ? 1 : 0)}"));

        public static string Apps(IEnumerable<InstalledApp> rows) =>
            string.Join(';', rows.Select(a => $"{Field(a.Id)}|{Field(a.Name)}|{Field(a.IconPath)}"));

        public static string Layouts(IEnumerable<KeyboardLayout> rows) =>
            string.Join(';', rows.Select(l => $"{Field(l.Id)}|{Field(l.DisplayName)}"));

        /// <summary>"percent|charging|pluggedIn|minutesLeft" - percent/minutes are -1 when unknown.</summary>
        public static string Battery(BatteryStatus b) =>
            $"{(b.HasBattery ? b.Percent : -1)}|{(b.Charging ? 1 : 0)}|{(b.PluggedIn ? 1 : 0)}|{(b.Remaining is { } r ? (int)r.TotalMinutes : -1)}";

        public static string Specs(SystemSpecs s) => string.Join('\n', new[]
        {
            $"OS: {s.OS}",
            $"CPU: {s.Cpu} ({s.CpuCores} threads)",
            $"Memory: {s.MemoryFreeMB} MB free of {s.MemoryTotalMB} MB",
            $"Uptime: {(int)s.Uptime.TotalDays}d {s.Uptime.Hours}h {s.Uptime.Minutes}m",
        }
        .Concat(s.Gpus.Select(g => $"GPU: {g}"))
        .Concat(s.Disks.Select(d => $"Disk: {d}"))
        .Concat(s.Network.Select(n => $"Network: {n}")));

        public static PowerAction ParsePower(string action) =>
            Enum.TryParse<PowerAction>(action, ignoreCase: true, out var a) && Enum.IsDefined(a)
                ? a
                : throw new ArgumentException($"Unknown power action '{action}'. Use one of: {string.Join(", ", Enum.GetNames<PowerAction>())}.");

        /// <summary>Flip one channel's mute and report the new state ("muted" / "unmuted").</summary>
        public static string ToggleMute(IAudioInterpreter audio, string channelId)
        {
            bool now;
            if (channelId == AudioChannel.MasterId) now = audio.GetMasterMute();
            else now = audio.ListChannels().FirstOrDefault(c => c.Id == channelId)?.Muted
                       ?? throw new KeyNotFoundException($"No audio channel '{channelId}' is playing right now.");
            audio.SetChannelMute(channelId, !now);
            return !now ? "muted" : "unmuted";
        }
    }
}
