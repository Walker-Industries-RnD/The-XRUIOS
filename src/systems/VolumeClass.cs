using static Pariah_Cybersecurity.DataHandler;
using static XRUIOS.Barebones.Interfaces.VolumeClass;
using static XRUIOS.Barebones.XRUIOS;

namespace XRUIOS.Barebones.Functions
{
    public static class VolumeClass
    {

        //AudioGroupClass deleted, this is pretty much that but better tbh
        //Remember we use te master volume in SoundEQ as a main volume switch!

      



        //Volumes are Dictionary<string, Dictionary<string, int>>
        //AKA Dictionary<Default, Dictionary<11234:Renderer:Calendar, 50>>
        //In the string, we format it as (MachineID:OSStyle:AppName OR AppID)

        internal static ObservableProperty<VolumeSetting> VolumeSettings;

        //CurrentSoundSetting (Get, Set)
        public static async Task<VolumeSetting> GetCurrentVolumeSettings()
        {
            return VolumeSettings;
        }

        public static async Task SetCurrentVolumeSettings(VolumeSetting soundSetting)
        {
            VolumeSettings.Set(soundSetting);
        }

        //Volume Settings (CRUD)
        //C
        public static async Task AddVolumeSettings(VolumeSetting VolumeMixings)
        {
            var directoryPath = Path.Combine(DataPath, "VolumeMixings");

            //Get the JSON File holding the MusicDirectory object for the user
            var FileWithVolumeSettingDB = await JSONDataHandler.LoadJsonFile(directoryPath, "VolumeMixings");
            var loaded = (List<VolumeSetting>)await JSONDataHandler.GetVariable<List<VolumeSetting>>(FileWithVolumeSettingDB, "VolumeMixings", encryptionKey);

            if (loaded.Any(d => d.GetHashCode() == VolumeMixings.GetHashCode()))
            {
                throw new InvalidOperationException("This already exists.");
            }

            loaded.Add(VolumeMixings);

            var updatedJSON = await JSONDataHandler.UpdateJson<List<VolumeSetting>>(FileWithVolumeSettingDB, "VolumeMixings", loaded, encryptionKey);

            await JSONDataHandler.SaveJson(updatedJSON);

        }

        //R
        public static async Task<List<VolumeSetting>> GetVolumeSettings()
        {
            var directoryPath = Path.Combine(DataPath, "VolumeMixings");

            //Get the JSON File holding the MusicDirectory object for the user
            var FileWithVolumeSettingDB = await JSONDataHandler.LoadJsonFile(directoryPath, "VolumeMixings");
            var loaded = (List<VolumeSetting>)await JSONDataHandler.GetVariable<List<VolumeSetting>>(FileWithVolumeSettingDB, "VolumeMixings", encryptionKey);

            return loaded;
        }

        public static async Task<VolumeSetting> GetVolumeSetting(string volumeSettingName)
        {
            var directoryPath = Path.Combine(DataPath, "VolumeMixings");

            var FileWithVolumeSettingDB = await JSONDataHandler.LoadJsonFile(directoryPath, "VolumeMixings");
            var loaded = (List<VolumeSetting>)await JSONDataHandler.GetVariable<List<VolumeSetting>>(FileWithVolumeSettingDB, "VolumeMixings", encryptionKey);
            var returnVal = loaded.FirstOrDefault(d => d.VolumeSettingName == volumeSettingName);

            return returnVal;
        }

        //App name and stuff/int for this device
        public static async Task<List<(string, int)>> GetVolumeSettingsForThisDevice()
        {

            var finalLine = new List<(string, int)>();


            var valToCheck = await GetCurrentVolumeSettings();

            foreach (var item in valToCheck.Volumes.Keys)
            {
                var split = item.Split(":");
                if (split[0] == Environment.MachineName)
                {
                    finalLine.Add((item, valToCheck.Volumes[item]));
                }
            }
            return finalLine;
        }

        //U
        public static async Task UpdateVolumeSettingDB(VolumeSetting originalData, VolumeSetting newData)
        {
            var directoryPath = Path.Combine(DataPath, "VolumeMixings");

            //Get the JSON File holding the MusicDirectory object for the user
            var FileWithVolumeSettingDB = await JSONDataHandler.LoadJsonFile(directoryPath, "VolumeMixings");
            var loaded = (List<VolumeSetting>)await JSONDataHandler.GetVariable<List<VolumeSetting>>(FileWithVolumeSettingDB, "VolumeMixings", encryptionKey);

            if (!loaded.Any(d => d.GetHashCode() == originalData.GetHashCode()))
            {
                throw new InvalidOperationException("This does not exist as a saved, stored item.");
            }

            var dataToRemove = loaded.First(d => d.GetHashCode() == originalData.GetHashCode());
            {
                loaded.Remove(dataToRemove);
                loaded.Add(newData);
            }

            var updatedJSON = await JSONDataHandler.UpdateJson<List<VolumeSetting>>(FileWithVolumeSettingDB, "VolumeMixings", loaded, encryptionKey);

            await JSONDataHandler.SaveJson(FileWithVolumeSettingDB);
        }


        //D
        public static async Task DeleteVolumeSettingDB(VolumeSetting deletedData)
        {
            var directoryPath = Path.Combine(DataPath, "VolumeMixings");

            //Get the JSON File holding the MusicDirectory object for the user
            var FileWithVolumeSettingDB = await JSONDataHandler.LoadJsonFile(directoryPath, "VolumeMixings");
            var loaded = (List<VolumeSetting>)await JSONDataHandler.GetVariable<List<VolumeSetting>>(FileWithVolumeSettingDB, "VolumeMixings", encryptionKey);

            if (!loaded.Any(d => d.GetHashCode() == deletedData.GetHashCode()))
            {
                throw new InvalidOperationException("This does not exist as a saved, stored item.");
            }

            var dataToRemove = loaded.First(d => d.GetHashCode() == deletedData.GetHashCode());
            {
                loaded.Remove(dataToRemove);
            }

            var updatedJSON = await JSONDataHandler.UpdateJson<List<VolumeSetting>>(FileWithVolumeSettingDB, "VolumeMixings", loaded, encryptionKey);

            await JSONDataHandler.SaveJson(FileWithVolumeSettingDB);
        }

        public static async Task ClearEQDB()
        {
            var directoryPath = Path.Combine(DataPath, "VolumeMixings");

            //Get the JSON File holding the MusicDirectory object for the user
            var FileWithVolumeSettingDB = await JSONDataHandler.LoadJsonFile(directoryPath, "VolumeMixings");
            var loaded = (List<VolumeSetting>)await JSONDataHandler.GetVariable<List<VolumeSetting>>(FileWithVolumeSettingDB, "VolumeMixings", encryptionKey);

            loaded.Clear();
            var updatedJSON = await JSONDataHandler.UpdateJson<List<VolumeSetting>>(FileWithVolumeSettingDB, "VolumeMixings", loaded, encryptionKey);

            await JSONDataHandler.SaveJson(FileWithVolumeSettingDB);
        }


    }

}
