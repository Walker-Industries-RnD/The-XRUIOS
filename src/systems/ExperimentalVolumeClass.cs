using System.Text.Json.Nodes;
using static Pariah_Cybersecurity.DataHandler;
using static XRUIOS.Barebones.Interfaces.ExperimentalAudioClass;
using static XRUIOS.Barebones.Interfaces.SoundEQClass;
using static XRUIOS.Barebones.XRUIOS;

namespace XRUIOS.Barebones
{
    public class ExperimentalAudioClass
    {






        public static int MasterVolume;
        public static ExperimentalAudio AdvancedAudioSettings;
        public static SoundEQ EQ;

        //Create "Save to file" and "Load from file" with presets
        public static class ExperimentalVolumeClass
        {

            public static ExperimentalAudio GetExperimentalAudioSettings()
            {
                return AdvancedAudioSettings;
            }


            public static void SetExperimentalAudioSettings(
               bool? EnvironmentalReduction = null,
               bool? DecibelLimit = null,
               int? EnvironmentalReductionPercentage = null,
               int? DecibelLimitLevel = null)
            {

                var updated = new ExperimentalAudio
                {
                    EnvironmentalReduction = EnvironmentalReduction ?? AdvancedAudioSettings.EnvironmentalReduction,
                    DecibelLimit = DecibelLimit ?? AdvancedAudioSettings.DecibelLimit,
                    EnvironmentalReductionPercentage = EnvironmentalReductionPercentage ?? AdvancedAudioSettings.EnvironmentalReductionPercentage,
                    DecibelLimitLevel = DecibelLimitLevel ?? AdvancedAudioSettings.DecibelLimitLevel
                };

                AdvancedAudioSettings = updated;
            }

            public static async Task SaveAudioSettings()
            {
                var directoryPath = Path.Combine(DataPath, "ExpAudio");

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);   
                }

                if (!File.Exists(Path.Combine(directoryPath, "ExpAudio.json")))
                {
                    await JSONDataHandler.CreateJsonFile(directoryPath, "ExpAudio", new JsonObject());
                }

                var json = await JSONDataHandler.LoadJsonFile("ExpAudio", directoryPath);

                if (await JSONDataHandler.CheckIfVariableExists(json, "Data"))
                {
                    json = await JSONDataHandler.UpdateJson<ExperimentalAudio>(json, "Data", AdvancedAudioSettings, encryptionKey);
                }

                else
                {
                    json = await JSONDataHandler.AddToJson<ExperimentalAudio>(json, "Data", AdvancedAudioSettings, encryptionKey);
                }

                await JSONDataHandler.SaveJson(json);


            }

            public static async Task LoadAudioSettings()
            {
                var directoryPath = Path.Combine(DataPath, "ExpAudio");

                var json = await JSONDataHandler.LoadJsonFile("ExpAudio", directoryPath);
                var loaded = (ExperimentalAudio)await JSONDataHandler.GetVariable<ExperimentalAudio>(json, "Data", encryptionKey);

                AdvancedAudioSettings = loaded;

            }


        }

        public static class MasterVolumeClass
        {
            public static int GetMasterVolume()
            {
                return MasterVolume;
            }

            public static void SetMasterVolume(int vol)
            {
                MasterVolume = vol;
            }

            public static async Task SaveAudioSettings()
            {
                var directoryPath = Path.Combine(DataPath, "MasterVol");

                if (!File.Exists(Path.Combine(directoryPath, "MasterVol.json")))
                {
                    await JSONDataHandler.CreateJsonFile(directoryPath, "MasterVol", new JsonObject());
                }

                var json = await JSONDataHandler.LoadJsonFile("MasterVol", directoryPath);

                if (await JSONDataHandler.CheckIfVariableExists(json, "Data"))
                {
                    json = await JSONDataHandler.UpdateJson<int>(json, "Data", MasterVolume, encryptionKey);
                }

                else
                {
                    json = await JSONDataHandler.AddToJson<int>(json, "Data", MasterVolume, encryptionKey);
                }

                await JSONDataHandler.SaveJson(json);


            }

            public static async Task LoadAudioSettings()
            {
                var directoryPath = Path.Combine(DataPath, "MasterVol");

                var json = await JSONDataHandler.LoadJsonFile("MasterVol", directoryPath);
                var loaded = (int)await JSONDataHandler.GetVariable<int>(json, "Data", encryptionKey);

                MasterVolume = loaded;

            }
        }




    }


}
