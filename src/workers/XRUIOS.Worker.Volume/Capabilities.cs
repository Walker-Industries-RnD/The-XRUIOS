using EclipseProject;
using XRUIOS.Interfaces;
using XRUIOS.Barebones.Functions;
using XRUIOS.Interpreter;
using static XRUIOS.Barebones.Interfaces.VolumeClass;

namespace XRUIOS.Worker.Volume
{
    // The Volume worker's cross-program surface. VolumeClass owns the saved mixes; the interpreter
    // turns a call into the real mixer change on this machine (Core Audio on Windows, PulseAudio /
    // PipeWire on Linux, the engine's buses under Aether). ListChannels/SetVolume/ToggleMute are what
    // the VolumeMixer hole calls.
    public static class VolumeCapabilities
    {
        private static IAudioInterpreter Audio => XruiosInterpreter.Current.Audio;

        [SeaOfDirac("Volume.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Volume: " + input;

        /// <summary>"id|name|volume|muted;..." - master first, then every app playing audio.</summary>
        [SeaOfDirac("ListChannels", new string[0], typeof(string))]
        public static string ListChannels() => Wire.Channels(Audio.ListChannels());

        // Numbers arrive as whatever MessagePack's smallest encoding decoded to (byte for 0-100);
        // long accepts all of them through reflection's widening conversion.
        [SeaOfDirac("SetVolume", new[] { "channelId", "volume" }, typeof(string), typeof(string), typeof(long))]
        public static string SetVolume(string channelId, long volume)
        {
            Audio.SetChannelVolume(channelId, (int)Math.Clamp(volume, 0, 100));
            Announce(channelId);
            return "ok";
        }

        /// <summary>Returns the new state: "muted" or "unmuted".</summary>
        [SeaOfDirac("ToggleMute", new[] { "channelId" }, typeof(string), typeof(string))]
        public static string ToggleMute(string channelId)
        {
            string state = Wire.ToggleMute(Audio, channelId);
            Announce(channelId);
            return state;
        }

        /// <summary>
        /// Save what's playing right now as a named mix. Keys follow VolumeClass's format,
        /// "MachineName:OS:AppName", so one saved mix can hold several devices.
        /// </summary>
        [SeaOfDirac("SaveVolumeSetting", new[] { "name" }, typeof(string), typeof(string))]
        public static async Task<string> SaveVolumeSetting(string name)
        {
            string prefix = $"{Environment.MachineName}:{XruiosInterpreter.Current.Platform}:";
            var existing = await VolumeClass.GetVolumeSetting(name);

            // Keep other machines' entries in an existing mix; replace only this machine's.
            var volumes = existing?.Volumes?
                .Where(kv => !kv.Key.StartsWith(Environment.MachineName + ":", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, int>();
            foreach (var c in Audio.ListChannels())
                volumes[prefix + (c.Id == AudioChannel.MasterId ? "Master" : c.Name)] = c.Volume;

            var setting = new VolumeSetting(name, volumes);
            if (existing is null) await VolumeClass.AddVolumeSettings(setting);
            else await VolumeClass.UpdateVolumeSettingDB(existing, setting);
            return $"saved {volumes.Count(kv => kv.Key.StartsWith(prefix))} channels as '{name}'";
        }

        /// <summary>
        /// Load a saved mix and make it real: this machine's entries are applied by app name (apps
        /// that aren't playing right now are skipped - their sessions don't exist yet).
        /// </summary>
        [SeaOfDirac("ApplyVolumeSetting", new[] { "name" }, typeof(string), typeof(string))]
        public static async Task<string> ApplyVolumeSetting(string name)
        {
            var setting = await VolumeClass.GetVolumeSetting(name)
                          ?? throw new KeyNotFoundException($"No saved volume setting '{name}'.");

            var live = Audio.ListChannels();
            int applied = 0, skipped = 0;
            foreach (var (key, volume) in setting.Volumes ?? new Dictionary<string, int>())
            {
                var parts = key.Split(':', 3);
                if (parts.Length != 3 || !parts[0].Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)) continue;

                string app = parts[2];
                var targets = app.Equals("Master", StringComparison.OrdinalIgnoreCase)
                    ? live.Where(c => c.Id == AudioChannel.MasterId)
                    : live.Where(c => c.Id != AudioChannel.MasterId && c.Name.Equals(app, StringComparison.OrdinalIgnoreCase));

                bool any = false;
                foreach (var c in targets)
                {
                    Audio.SetChannelVolume(c.Id, volume);
                    WorkerEvents.Publish(XruiosEventNames.VolumeChanged, EventJson.Write(new VolumeChange(c.Id, volume, c.Muted)));
                    any = true;
                }
                if (any) applied++; else skipped++;
            }
            return skipped == 0 ? $"applied {applied}" : $"applied {applied}, skipped {skipped} (not playing)";
        }

        /// <summary>Tell listeners (a volume HUD, the mixer app) where a channel ended up. Best effort.</summary>
        private static void Announce(string channelId)
        {
            try
            {
                var c = Audio.ListChannels().FirstOrDefault(x => x.Id == channelId);
                if (c != null) WorkerEvents.Publish(XruiosEventNames.VolumeChanged, EventJson.Write(new VolumeChange(c.Id, c.Volume, c.Muted)));
            }
            catch { /* the change itself succeeded; a missed announcement isn't worth failing the call */ }
        }

        /// <summary>Saved mix names, ';'-separated.</summary>
        [SeaOfDirac("ListVolumeSettings", new string[0], typeof(string))]
        public static async Task<string> ListVolumeSettings()
            => string.Join(';', (await VolumeClass.GetVolumeSettings()).Select(v => Wire.Field(v.VolumeSettingName)));
    }
}
