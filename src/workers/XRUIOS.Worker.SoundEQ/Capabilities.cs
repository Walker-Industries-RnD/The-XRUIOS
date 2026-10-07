using EclipseProject;
using XRUIOS.Interfaces;
using XRUIOS.Interpreter;
using EqPreset = XRUIOS.Barebones.Interfaces.SoundEQClass.SoundEQ;
using EqStore = XRUIOS.Barebones.SoundEQClass;
using ExperimentalAudio = XRUIOS.Barebones.Interfaces.ExperimentalAudioClass.ExperimentalAudio;

namespace XRUIOS.Worker.SoundEQ
{
    // Named EQ presets per sound category (Software / Effects / Voice / Music / Alerts / UI / Etc).
    // Developer-owned: the OS mixer has no such categories, so the worker stores the presets and
    // announces EqChanged when one is applied; the host's IAudioEffectsInterpreter maps the categories
    // onto its own audio buses.
    public static class SoundEQCapabilities
    {
        private static string ActiveFile => Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "EQDB", "active");

        [SeaOfDirac("SoundEQ.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.SoundEQ: " + input;

        /// <summary>Saved preset names, ';'-separated.</summary>
        [SeaOfDirac("ListEqs", new string[0], typeof(string))]
        public static async Task<string> ListEqs() =>
            string.Join(';', (await EqStore.GetSoundEQDBs()).Select(e => Wire.Field(e.EQName)));

        /// <summary>Create or replace a preset. Levels are 0-100 per category.</summary>
        [SeaOfDirac("SaveEq", new[] { "name", "software", "effects", "voice", "music", "alerts", "ui", "etc" },
            typeof(string), typeof(string), typeof(double), typeof(double), typeof(double), typeof(double), typeof(double), typeof(double), typeof(double))]
        public static async Task<string> SaveEq(string name, double software, double effects, double voice, double music,
            double alerts, double ui, double etc)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A preset needs a name.");
            static float L(double v) => (float)Math.Clamp(v, 0, 100);
            var preset = new EqPreset(name, L(software), L(effects), L(voice), L(music), L(alerts), L(ui), L(etc),
                new ExperimentalAudio(false, false, 0, 0));

            var existing = await EqStore.GetSoundEQDB(name);
            if (existing is null) await EqStore.AddSoundEQDBs(preset);
            else await EqStore.UpdateSoundEQDB(existing, preset);
            return "saved";
        }

        /// <summary>Make a preset active and announce it (EqChanged) for the host to apply.</summary>
        [SeaOfDirac("ApplyEq", new[] { "name" }, typeof(string), typeof(string))]
        public static async Task<string> ApplyEq(string name)
        {
            var preset = await EqStore.GetSoundEQDB(name) ?? throw new KeyNotFoundException($"No EQ preset '{name}'.");
            await File.WriteAllTextAsync(ActiveFile, name);
            string json = EventJson.Write(Info(preset));
            WorkerEvents.Publish(XruiosEventNames.EqChanged, json);
            return json;
        }

        /// <summary>The active preset as JSON, or "" when none has been applied.</summary>
        [SeaOfDirac("GetActiveEq", new string[0], typeof(string))]
        public static async Task<string> GetActiveEq()
        {
            if (!File.Exists(ActiveFile)) return "";
            var preset = await EqStore.GetSoundEQDB((await File.ReadAllTextAsync(ActiveFile)).Trim());
            return preset is null ? "" : EventJson.Write(Info(preset));
        }

        private static EqSettings Info(EqPreset p) =>
            new(p.EQName, p.Software, p.Effects, p.Voice, p.Music, p.Alerts, p.UI, p.Etc);
    }
}
