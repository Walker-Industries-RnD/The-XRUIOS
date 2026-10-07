using EclipseProject;
using XRUIOS.Interfaces;
using XRUIOS.Interpreter;
using static XRUIOS.Barebones.ExperimentalAudioClass;
using ExperimentalAudio = XRUIOS.Barebones.Interfaces.ExperimentalAudioClass.ExperimentalAudio;

namespace XRUIOS.Worker.ExperimentalVolume
{
    // Master volume as a saved setting: MasterVolumeClass stores the number, the interpreter makes the
    // OS output actually match it. ("Remember we use the master volume in SoundEQ as a main volume
    // switch" - this is that switch.)
    //
    // The experimental settings (environmental reduction, decibel limit) are developer-owned: the OS
    // mixer can't do them, so they're stored here and announced as ExperimentalAudioChanged for the
    // host's IAudioEffectsInterpreter (its own audio engine) to apply.
    public static class ExperimentalVolumeCapabilities
    {
        private static IAudioInterpreter Audio => XruiosInterpreter.Current.Audio;

        [SeaOfDirac("ExperimentalVolume.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.ExperimentalVolume: " + input;

        /// <summary>The OS's current master volume (0-100).</summary>
        [SeaOfDirac("GetMasterVolume", new string[0], typeof(string))]
        public static string GetMasterVolume() => Audio.GetMasterVolume().ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Set the OS master volume AND remember it, so RestoreMasterVolume can bring it back.</summary>
        [SeaOfDirac("SetMasterVolume", new[] { "volume" }, typeof(string), typeof(long))]
        public static async Task<string> SetMasterVolume(long volume)
        {
            int v = (int)Math.Clamp(volume, 0, 100);
            Audio.SetMasterVolume(v);
            MasterVolumeClass.SetMasterVolume(v);
            await MasterVolumeClass.SaveAudioSettings();
            AnnounceMaster(v);
            return "ok";
        }

        /// <summary>Load the saved master volume and apply it to the OS. Returns the value applied.</summary>
        [SeaOfDirac("RestoreMasterVolume", new string[0], typeof(string))]
        public static async Task<string> RestoreMasterVolume()
        {
            if (!File.Exists(Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "MasterVol", "MasterVol.json")))
                throw new InvalidOperationException("No master volume has been saved yet (call SetMasterVolume first).");
            await MasterVolumeClass.LoadAudioSettings();
            int v = Math.Clamp(MasterVolumeClass.GetMasterVolume(), 0, 100);
            Audio.SetMasterVolume(v);
            AnnounceMaster(v);
            return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The saved experimental audio settings as JSON (all off until someone sets them).</summary>
        [SeaOfDirac("GetExperimentalAudio", new string[0], typeof(string))]
        public static async Task<string> GetExperimentalAudio() => EventJson.Write(ToInfo(await LoadExperimentalAsync()));

        [SeaOfDirac("SetExperimentalAudio",
            new[] { "environmentalReduction", "environmentalReductionPercentage", "decibelLimit", "decibelLimitLevel" },
            typeof(string), typeof(bool), typeof(long), typeof(bool), typeof(long))]
        public static async Task<string> SetExperimentalAudio(bool environmentalReduction, long environmentalReductionPercentage,
            bool decibelLimit, long decibelLimitLevel)
        {
            ExperimentalVolumeClass.SetExperimentalAudioSettings(
                environmentalReduction, decibelLimit,
                (int)Math.Clamp(environmentalReductionPercentage, 0, 100), (int)Math.Clamp(decibelLimitLevel, 0, 200));
            await ExperimentalVolumeClass.SaveAudioSettings();

            string json = EventJson.Write(ToInfo(ExperimentalVolumeClass.GetExperimentalAudioSettings()));
            WorkerEvents.Publish(XruiosEventNames.ExperimentalAudioChanged, json);
            return json;
        }

        private static async Task<ExperimentalAudio> LoadExperimentalAsync()
        {
            if (File.Exists(Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "ExpAudio", "ExpAudio.json")))
                await ExperimentalVolumeClass.LoadAudioSettings();
            return ExperimentalVolumeClass.GetExperimentalAudioSettings() ?? new ExperimentalAudio(false, false, 0, 0);
        }

        private static ExperimentalAudioSettings ToInfo(ExperimentalAudio a) =>
            new(a.EnvironmentalReduction, a.EnvironmentalReductionPercentage, a.DecibelLimit, a.DecibelLimitLevel);

        private static void AnnounceMaster(int volume)
        {
            try { WorkerEvents.Publish(XruiosEventNames.VolumeChanged, EventJson.Write(new VolumeChange(AudioChannel.MasterId, volume, Audio.GetMasterMute()))); }
            catch { /* best effort */ }
        }
    }
}
