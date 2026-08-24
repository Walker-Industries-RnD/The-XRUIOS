using static Pariah_Cybersecurity.DataHandler;
using static XRUIOS.Barebones.XRUIOS;
using YuukoProtocol;


namespace XRUIOS.Barebones
{
    public static class RecentlyRecordedClass
    {
        private const int MaxRecent = 30;

        public static async Task<List<FileRecord>> GetRecentlyRecorded()
        {
            var directoryPath = Path.Combine(DataPath, "RecentlyRecorded");
            var file = await JSONDataHandler.LoadJsonFile(directoryPath, "RecentlyRecorded");
            var loaded = (List<FileRecord>)await JSONDataHandler.GetVariable<List<FileRecord>>(file, "RecentlyRecorded", encryptionKey);
            return loaded;
        }

        public static async Task AddToRecentlyRecorded(FileRecord newlyRecorded)
        {
            var directoryPath = Path.Combine(DataPath, "RecentlyRecorded");
            var file = await JSONDataHandler.LoadJsonFile(directoryPath, "RecentlyRecorded");
            var loaded = (List<FileRecord>)await JSONDataHandler.GetVariable<List<FileRecord>>(file, "RecentlyRecorded", encryptionKey);

            // Remove oldest if over limit
            if (loaded.Count >= MaxRecent)
                loaded.RemoveAt(0);

            loaded.Add(newlyRecorded);

            var updatedJSON = await JSONDataHandler.UpdateJson<List<FileRecord>>(file, "RecentlyRecorded", loaded, encryptionKey);
            await JSONDataHandler.SaveJson(updatedJSON);
        }

        public static async Task DeleteSoundRecentlyRecorded(FileRecord deletedData)
        {
            var directoryPath = Path.Combine(DataPath, "RecentlyRecorded");
            var file = await JSONDataHandler.LoadJsonFile(directoryPath, "RecentlyRecorded");
            var loaded = (List<FileRecord>)await JSONDataHandler.GetVariable<List<FileRecord>>(file, "RecentlyRecorded", encryptionKey);

            var item = loaded.FirstOrDefault(d => d.GetHashCode() == deletedData.GetHashCode());
            if (item == null)
                throw new InvalidOperationException("This does not exist as a saved, stored item.");

            loaded.Remove(item);

            var updatedJSON = await JSONDataHandler.UpdateJson<List<FileRecord>>(file, "RecentlyRecorded", loaded, encryptionKey);
            await JSONDataHandler.SaveJson(updatedJSON);
        }

        public static async Task ClearRecentlyRecorded()
        {
            var directoryPath = Path.Combine(DataPath, "RecentlyRecorded");
            var file = await JSONDataHandler.LoadJsonFile(directoryPath, "RecentlyRecorded");
            var loaded = (List<FileRecord>)await JSONDataHandler.GetVariable<List<FileRecord>>(file, "RecentlyRecorded", encryptionKey);

            loaded.Clear();
            var updatedJSON = await JSONDataHandler.UpdateJson<List<FileRecord>>(file, "RecentlyRecorded", loaded, encryptionKey);
            await JSONDataHandler.SaveJson(updatedJSON);
        }
    }
}
