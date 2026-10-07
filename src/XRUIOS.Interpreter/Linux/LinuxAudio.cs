using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XRUIOS.Interpreter.Linux
{
    /// <summary>
    /// Master = the default sink. Backends, in order: pactl (PulseAudio, and PipeWire through
    /// pipewire-pulse - the only one with per-app streams), wpctl (native PipeWire), amixer (bare ALSA).
    /// Per-app channels are PulseAudio sink-inputs, id "sink-input:&lt;index&gt;".
    /// </summary>
    [SupportedOSPlatform("linux")]
    internal sealed partial class LinuxAudio : IAudioInterpreter
    {
        private const string SinkInputPrefix = "sink-input:";

        private enum Backend { Pactl, Wpctl, Amixer, None }

        private static Backend Pick() =>
            Shell.Has("pactl") ? Backend.Pactl :
            Shell.Has("wpctl") ? Backend.Wpctl :
            Shell.Has("amixer") ? Backend.Amixer : Backend.None;

        private static Exception NoBackend() =>
            new PlatformNotSupportedException("No audio control tool found. Install pulseaudio-utils (pactl), wireplumber (wpctl) or alsa-utils (amixer).");

        public int GetMasterVolume() => Pick() switch
        {
            Backend.Pactl => FirstPercent(Shell.RunChecked("pactl", new[] { "get-sink-volume", "@DEFAULT_SINK@" }, cLocale: true)),
            Backend.Wpctl => WpctlVolume(Shell.RunChecked("wpctl", new[] { "get-volume", "@DEFAULT_AUDIO_SINK@" }, cLocale: true)).volume,
            Backend.Amixer => FirstPercent(Shell.RunChecked("amixer", new[] { "-M", "get", "Master" }, cLocale: true)),
            _ => throw NoBackend(),
        };

        public void SetMasterVolume(int volume0to100)
        {
            int v = Shell.Clamp01to100(volume0to100);
            switch (Pick())
            {
                case Backend.Pactl: Shell.RunChecked("pactl", new[] { "set-sink-volume", "@DEFAULT_SINK@", $"{v}%" }); break;
                case Backend.Wpctl: Shell.RunChecked("wpctl", new[] { "set-volume", "@DEFAULT_AUDIO_SINK@", (v / 100.0).ToString("0.00", CultureInfo.InvariantCulture) }); break;
                case Backend.Amixer: Shell.RunChecked("amixer", new[] { "-M", "-q", "set", "Master", $"{v}%" }); break;
                default: throw NoBackend();
            }
        }

        public bool GetMasterMute() => Pick() switch
        {
            Backend.Pactl => Shell.RunChecked("pactl", new[] { "get-sink-mute", "@DEFAULT_SINK@" }, cLocale: true).Contains("yes", StringComparison.OrdinalIgnoreCase),
            Backend.Wpctl => WpctlVolume(Shell.RunChecked("wpctl", new[] { "get-volume", "@DEFAULT_AUDIO_SINK@" }, cLocale: true)).muted,
            Backend.Amixer => Shell.RunChecked("amixer", new[] { "get", "Master" }, cLocale: true).Contains("[off]"),
            _ => throw NoBackend(),
        };

        public void SetMasterMute(bool muted)
        {
            switch (Pick())
            {
                case Backend.Pactl: Shell.RunChecked("pactl", new[] { "set-sink-mute", "@DEFAULT_SINK@", muted ? "1" : "0" }); break;
                case Backend.Wpctl: Shell.RunChecked("wpctl", new[] { "set-mute", "@DEFAULT_AUDIO_SINK@", muted ? "1" : "0" }); break;
                case Backend.Amixer: Shell.RunChecked("amixer", new[] { "-q", "set", "Master", muted ? "mute" : "unmute" }); break;
                default: throw NoBackend();
            }
        }

        public IReadOnlyList<AudioChannel> ListChannels()
        {
            var rows = new List<AudioChannel> { new(AudioChannel.MasterId, "Master", GetMasterVolume(), GetMasterMute()) };
            if (Shell.Has("pactl")) rows.AddRange(SinkInputs());
            return rows;
        }

        public void SetChannelVolume(string channelId, int volume0to100)
        {
            if (channelId == AudioChannel.MasterId) { SetMasterVolume(volume0to100); return; }
            Shell.RunChecked("pactl", new[] { "set-sink-input-volume", SinkInputIndex(channelId), $"{Shell.Clamp01to100(volume0to100)}%" });
        }

        public void SetChannelMute(string channelId, bool muted)
        {
            if (channelId == AudioChannel.MasterId) { SetMasterMute(muted); return; }
            Shell.RunChecked("pactl", new[] { "set-sink-input-mute", SinkInputIndex(channelId), muted ? "1" : "0" });
        }

        private static string SinkInputIndex(string channelId)
        {
            if (!Shell.Has("pactl"))
                throw new PlatformNotSupportedException("Per-app volume needs pactl (pulseaudio-utils, or pipewire-pulse).");
            if (!channelId.StartsWith(SinkInputPrefix, StringComparison.Ordinal) ||
                !uint.TryParse(channelId.AsSpan(SinkInputPrefix.Length), out uint index))
                throw new KeyNotFoundException($"'{channelId}' is not an audio channel id.");
            return index.ToString(CultureInfo.InvariantCulture);
        }

        // ── pactl sink-inputs ────────────────────────────────────────────────────────────────────

        private static List<AudioChannel> SinkInputs()
        {
            // Newer pactl (PulseAudio >= 16 / pipewire-pulse) speaks JSON; older only the text dump.
            var json = Shell.Run("pactl", new[] { "-f", "json", "list", "sink-inputs" }, cLocale: true);
            if (json.Ok && json.StdOut.TrimStart().StartsWith('['))
            {
                try { return ParseSinkInputsJson(json.StdOut); }
                catch (JsonException) { /* fall back to the text dump */ }
            }
            return ParseSinkInputsText(Shell.RunChecked("pactl", new[] { "list", "sink-inputs" }, cLocale: true));
        }

        internal static List<AudioChannel> ParseSinkInputsJson(string text)
        {
            var rows = new List<AudioChannel>();
            using var doc = JsonDocument.Parse(text);
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                int index = e.GetProperty("index").GetInt32();
                bool muted = e.TryGetProperty("mute", out var m) && m.ValueKind == JsonValueKind.True;

                int volume = 0;
                if (e.TryGetProperty("volume", out var vol) && vol.ValueKind == JsonValueKind.Object)
                {
                    var channelPercents = vol.EnumerateObject()
                        .Select(c => c.Value.TryGetProperty("value_percent", out var p) ? FirstPercent(p.GetString() ?? "") : 0)
                        .ToList();
                    if (channelPercents.Count > 0) volume = channelPercents.Max();
                }

                string name = $"Stream {index}";
                if (e.TryGetProperty("properties", out var props))
                    name = Prop(props, "application.name") ?? Prop(props, "application.process.binary") ?? Prop(props, "media.name") ?? name;

                rows.Add(new AudioChannel(SinkInputPrefix + index, name, volume, muted));
            }
            return rows;

            static string? Prop(JsonElement props, string key) =>
                props.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && v.GetString()!.Length > 0 ? v.GetString() : null;
        }

        internal static List<AudioChannel> ParseSinkInputsText(string text)
        {
            var rows = new List<AudioChannel>();
            int? index = null;
            int volume = 0;
            bool muted = false;
            string? appName = null, binary = null;

            void Flush()
            {
                if (index is int i)
                    rows.Add(new AudioChannel(SinkInputPrefix + i, appName ?? binary ?? $"Stream {i}", volume, muted));
                index = null; volume = 0; muted = false; appName = null; binary = null;
            }

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("Sink Input #", StringComparison.Ordinal))
                {
                    Flush();
                    if (int.TryParse(line.AsSpan("Sink Input #".Length), out int i)) index = i;
                }
                else if (line.StartsWith("Mute:", StringComparison.Ordinal)) muted = line.Contains("yes");
                else if (line.StartsWith("Volume:", StringComparison.Ordinal)) volume = FirstPercent(line);
                else if (line.StartsWith("application.name = ", StringComparison.Ordinal)) appName = Unquote(line);
                else if (line.StartsWith("application.process.binary = ", StringComparison.Ordinal)) binary = Unquote(line);
            }
            Flush();
            return rows;

            static string Unquote(string line) => line[(line.IndexOf('=') + 1)..].Trim().Trim('"');
        }

        // ── parsing helpers ──────────────────────────────────────────────────────────────────────

        [GeneratedRegex(@"(\d+)%")] private static partial Regex PercentRx();
        [GeneratedRegex(@"Volume:\s*([0-9.]+)")] private static partial Regex WpctlRx();

        internal static int FirstPercent(string text)
        {
            var m = PercentRx().Match(text);
            if (!m.Success) throw new FormatException($"No volume percentage in: {Shell.FirstLine(text)}");
            return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        internal static (int volume, bool muted) WpctlVolume(string text)
        {
            var m = WpctlRx().Match(text);
            if (!m.Success) throw new FormatException($"Unexpected wpctl output: {Shell.FirstLine(text)}");
            double level = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            return ((int)Math.Round(level * 100), text.Contains("[MUTED]"));
        }
    }
}
