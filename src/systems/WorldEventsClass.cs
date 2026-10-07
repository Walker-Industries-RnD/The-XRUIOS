using static Pariah_Cybersecurity.DataHandler;
using static XRUIOS.Barebones.Interfaces.WorldEventsClass;
using static XRUIOS.Barebones.XRUIOS;

namespace XRUIOS.Barebones
{
    public static class WorldEventsClass
    {
        private const int MaxEvents = 1000;

      
        public static async Task<List<WorldEvent>> GetWorldEvents()
        {
            var directoryPath = Path.Combine(DataPath, "WorldEvents");
                        var file = await JSONDataHandler.LoadJsonFile("WorldEvents", directoryPath);
            var loaded = (List<WorldEvent>)await JSONDataHandler.GetVariable<List<WorldEvent>>(file, "WorldEvents", encryptionKey);
            return loaded;
        }

        public static async Task AddWorldEvent(WorldEvent newEvent)
        {
            var directoryPath = Path.Combine(DataPath, "WorldEvents");
            var file = await JSONDataHandler.LoadJsonFile("WorldEvents", directoryPath);
            var loaded = (List<WorldEvent>)await JSONDataHandler.GetVariable<List<WorldEvent>>(file, "WorldEvents", encryptionKey);

            // Remove oldest if over limit
            if (loaded.Count >= MaxEvents)
                loaded.RemoveAt(0);

            // Generate ID if not provided
            if (string.IsNullOrEmpty(newEvent.EventId))
            {
                newEvent = newEvent with { EventId = Guid.NewGuid().ToString() };
            }

            // Ensure UTC
            if (newEvent.Timestamp.Kind != DateTimeKind.Utc)
            {
                newEvent = newEvent with { Timestamp = newEvent.Timestamp.ToUniversalTime() };
            }

            loaded.Add(newEvent);

            var updatedJSON = await JSONDataHandler.UpdateJson<List<WorldEvent>>(file, "WorldEvents", loaded, encryptionKey);
            await JSONDataHandler.SaveJson(updatedJSON);
        }

        public static async Task DeleteWorldEvent(WorldEvent deletedEvent)
        {
            var directoryPath = Path.Combine(DataPath, "WorldEvents");
                        var file = await JSONDataHandler.LoadJsonFile("WorldEvents", directoryPath);
            var loaded = (List<WorldEvent>)await JSONDataHandler.GetVariable<List<WorldEvent>>(file, "WorldEvents", encryptionKey);

            var item = loaded.FirstOrDefault(d => d.EventId == deletedEvent.EventId);
            if (item == null)
                throw new InvalidOperationException("This does not exist as a saved, stored item.");

            loaded.Remove(item);

            var updatedJSON = await JSONDataHandler.UpdateJson<List<WorldEvent>>(file, "WorldEvents", loaded, encryptionKey);
            await JSONDataHandler.SaveJson(updatedJSON);
        }

        public static async Task ClearWorldEvents()
        {
            var directoryPath = Path.Combine(DataPath, "WorldEvents");
                        var file = await JSONDataHandler.LoadJsonFile("WorldEvents", directoryPath);
            var loaded = (List<WorldEvent>)await JSONDataHandler.GetVariable<List<WorldEvent>>(file, "WorldEvents", encryptionKey);

            loaded.Clear();
            var updatedJSON = await JSONDataHandler.UpdateJson<List<WorldEvent>>(file, "WorldEvents", loaded, encryptionKey);
            await JSONDataHandler.SaveJson(updatedJSON);
        }

    }
}