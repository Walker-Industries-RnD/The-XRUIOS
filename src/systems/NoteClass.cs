using Microsoft.Maui.Storage;
using Pariah_Cybersecurity;
using System.Text.Json.Nodes;
using static Pariah_Cybersecurity.DataHandler;
using static Pariah_Cybersecurity.DataHandler.JSONDataHandler;
using static XRUIOS.Barebones.Interfaces.NoteClass;
using static XRUIOS.Barebones.XRUIOS;

namespace XRUIOS.Barebones.Functions
{
    public static class NoteClass
    {

 
        //C
        //Remember to create a folder with the same name in the directory containing all the assets!

        public static async Task SaveJournal(Journal journal)
        {
            var directoryPath = Path.Combine(DataPath, "Journals");

            Directory.CreateDirectory(directoryPath);

            var fileName = $"{journal.Identity.Name} v{journal.Identity.Version} by {journal.Identity.Author}__ID {journal.Identity.ThemeID}";

            var filePath = Path.Combine(DataPath, "Journals", fileName);

            if (File.Exists(filePath))
            {
                throw new InvalidOperationException("This journal already exists; please change the name.");
            }

            await JSONDataHandler.CreateJsonFile(fileName, directoryPath, new JsonObject());

            var json = await JSONDataHandler.LoadJsonFile(fileName, directoryPath);
            json = await JSONDataHandler.AddToJson<Journal>(json, "Data", journal, encryptionKey);
            await JSONDataHandler.SaveJson(json);
        }

        //R
        public static async Task<List<Journal>> GetAllJournals()
        {
            List<Journal> Journals = new List<Journal>();

            var directoryPath = Path.Combine(DataPath, "Journals");

            var themePaths = Directory.EnumerateFiles(directoryPath);

            foreach (var item in themePaths)
            {
                var json = await JSONDataHandler.LoadJsonFile((Path.GetFileNameWithoutExtension(item)), directoryPath);

                var themeFile = (Journal)await JSONDataHandler.GetVariable<Journal>(json, "Data", encryptionKey);

                Journals.Add(themeFile);

            }

            return Journals;
        }

        public static async Task<Journal> GetJournal(string FileName)
        {

            var directoryPath = Path.Combine(DataPath, "Journals");

            var json = await JSONDataHandler.LoadJsonFile(FileName, directoryPath);

            var journalFile = (Journal)await JSONDataHandler.GetVariable<Journal>(json, "Data", encryptionKey);

            return journalFile;
        }


        public static async Task<Category> GetCategory(string JournalName, string CategoryName)
        {
            var funcjournal = await GetJournal(JournalName);
            foreach (Category item in funcjournal.Categories)
            {
                if (item.Title == CategoryName)
                {
                    return item;
                }
            }

            throw new InvalidOperationException("Category not found.");
        }


        //U

        //Remember to put Identity.Version up
        public static async Task<string> UpdateJournal(Journal journal, Journal newJournal)
        {
            var directoryPath = Path.Combine(DataPath, "Journals");

            var oldFileName = $"{journal.Identity.Name} v{journal.Identity.Version} by {journal.Identity.Author}__ID {journal.Identity.ThemeID}";
            var newFileName = $"{newJournal.Identity.Name} v{newJournal.Identity.Version} by {newJournal.Identity.Author}__ID {newJournal.Identity.ThemeID}";

            var oldFilePath = Path.Combine(directoryPath, oldFileName + ".json");    // ← add .json !!
            var newFilePath = Path.Combine(directoryPath, newFileName + ".json");

            // 1. Verify old file actually exists
            if (!File.Exists(oldFilePath))
                throw new FileNotFoundException($"Cannot update: original journal file not found at {oldFilePath}");

            PariahJSON json;

            // 2. Load original
            try
            {
                json = await JSONDataHandler.LoadJsonFile(oldFileName, directoryPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load original journal {oldFileName}", ex);
            }

            bool renamed = false;

            // 3. Rename only if necessary
            if (oldFileName != newFileName)
            {
                try
                {
                    File.Move(oldFilePath, newFilePath, overwrite: false);
                    renamed = true;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to rename journal from {oldFileName} → {newFileName}", ex);
                }
            }

            // 4. Now load whatever file we should be working with
            string targetFileName = renamed ? newFileName : oldFileName;
            json = await JSONDataHandler.LoadJsonFile(targetFileName, directoryPath);

            // 5. Update content
            json = await JSONDataHandler.UpdateJson<Journal>(json, "Data", newJournal, encryptionKey);
            await JSONDataHandler.SaveJson(json);


            return newFileName;
        }


        //D
        public static async Task DeleteJournal(string fileName)
        {
            var directoryPath = Path.Combine(DataPath, "Journals");
            var filePath = Path.Combine(directoryPath, fileName + ".json"); 

            if (!File.Exists(filePath))
                throw new InvalidOperationException($"Journal file not found: {filePath}");

            try
            {
                File.Delete(filePath);
            }

            catch (Exception ex)
            {
                throw new Exception($"Failed to delete journal {fileName}: {ex.Message}", ex);
            }
        }


        //Favorites

        // C
        public static async Task AddJournalToFavorites(string journalId)
        {
            var dir = Path.Combine(DataPath, "Journals");

            // Verify journal exists
            var matchingFile = Directory.EnumerateFiles(dir, journalId + ".*").FirstOrDefault();
            if (matchingFile == null)
                throw new FileNotFoundException($"Journal not found: {journalId}");

            const string FavoritesFileName = "JournalFavorites";
            var favFilePath = Path.Combine(dir, FavoritesFileName + ".json");

            PariahJSON favJson;

            if (!File.Exists(favFilePath))
            {
                // First time ever → create empty favorites file
                await JSONDataHandler.CreateJsonFile(FavoritesFileName, dir, new JsonObject());

                // Immediately initialize the "Data" key with empty list
                favJson = await JSONDataHandler.LoadJsonFile(FavoritesFileName, dir);
                favJson = await JSONDataHandler.AddToJson<List<string>>(favJson, "Data", new List<string>(), encryptionKey);
                await JSONDataHandler.SaveJson(favJson);
            }
            else
            {
                favJson = await JSONDataHandler.LoadJsonFile(FavoritesFileName, dir);
            }

            var favorites = await JSONDataHandler.GetVariable<List<string>>(favJson, "Data", encryptionKey)
                as List<string> ?? new List<string>();

            if (favorites.Contains(journalId))
                throw new InvalidOperationException("Already favorited.");

            favorites.Add(journalId);

            var updatedJson = await JSONDataHandler.UpdateJson<List<string>>(favJson, "Data", favorites, encryptionKey);
            await JSONDataHandler.SaveJson(updatedJson);
        }


        // R
        public static async Task<(List<string> resolved, List<string> unresolved)> GetJournalFavorites()
        {
            var directoryPath = Path.Combine(DataPath, "Journals");

            var favoritesFile =
                await DataHandler.JSONDataHandler.LoadJsonFile("JournalFavorites", directoryPath);

            var favorites =
                (List<string>)await DataHandler.JSONDataHandler.GetVariable<List<string>>(
                    favoritesFile, "Data", encryptionKey);

            List<string> resolved = new();
            List<string> unresolved = new();

            foreach (var journalId in favorites)
            {
                var exists = Directory.EnumerateFiles(directoryPath, journalId + ".*").Any();

                if (exists)
                    resolved.Add(journalId);
                else
                    unresolved.Add(journalId);
            }

            return (resolved, unresolved);
        }

        public static async Task<List<string>> GetFavoriteJournalIdsAsync(bool onlyResolved = true)
        {
            var (resolved, unresolved) = await GetJournalFavorites();

            if (onlyResolved)
                return resolved;

            var all = new List<string>(resolved);
            all.AddRange(unresolved);
            return all;
        }

        // D
        public static async Task RemoveJournalFromFavorites(string JournalId)
        {
            var directoryPath = Path.Combine(DataPath, "Journals");

            var favoritesFile =
                await DataHandler.JSONDataHandler.LoadJsonFile("JournalFavorites", directoryPath);

            var favorites =
                (List<string>)await DataHandler.JSONDataHandler.GetVariable<List<string>>(
                    favoritesFile, "Data", encryptionKey);

            if (!favorites.Remove(JournalId))
                throw new InvalidOperationException("Journal not favorited.");

            var editedJSON =
                await DataHandler.JSONDataHandler.UpdateJson<List<string>>(
                    favoritesFile, "Data", favorites, encryptionKey);

            await DataHandler.JSONDataHandler.SaveJson(editedJSON);
        }


  

        //C
        public static async Task AddHistoryEntry(
            string TargetType,
            string Action,
            string TargetID,
            Dictionary<string, string>? Meta = null)
        {
            var directoryPath = Path.Combine(DataPath, "Journals");
            Directory.CreateDirectory(directoryPath);

            const string HistoryFileName = "History";
            var historyPath = Path.Combine(directoryPath, HistoryFileName + ".json");

            PariahJSON historyJson;

            if (!File.Exists(historyPath))
            {
                await JSONDataHandler.CreateJsonFile(HistoryFileName, directoryPath, new JsonObject());
                historyJson = await JSONDataHandler.LoadJsonFile(HistoryFileName, directoryPath);
                historyJson = await JSONDataHandler.AddToJson<List<HistoryEntry>>(historyJson, "Data", new List<HistoryEntry>(), encryptionKey);
                await JSONDataHandler.SaveJson(historyJson);
            }
            else
            {
                historyJson = await JSONDataHandler.LoadJsonFile(HistoryFileName, directoryPath);
            }

            var entries = await JSONDataHandler.GetVariable<List<HistoryEntry>>(historyJson, "Data", encryptionKey)
                as List<HistoryEntry> ?? new List<HistoryEntry>();

            entries.Add(new HistoryEntry(Action, TargetType, TargetID, DateTime.UtcNow, Meta));

            var editedJSON = await JSONDataHandler.UpdateJson<List<HistoryEntry>>(historyJson, "Data", entries, encryptionKey);
            await JSONDataHandler.SaveJson(editedJSON);
        }

        //R
        public static async Task<List<HistoryEntry>> GetHistory(
            string TargetType,
            string? TargetID = null)
        {
            var directoryPath = Path.Combine(DataPath, "Journals");

            var historyFile =
                await JSONDataHandler.LoadJsonFile("History", directoryPath);

            var entries =
                (List<HistoryEntry>)await JSONDataHandler.GetVariable<List<HistoryEntry>>(
                    historyFile, "Data", encryptionKey);

            if (TargetID == null)
            {
                return entries;
            }

            List<HistoryEntry> filtered = new();

            foreach (var item in entries)
            {
                if (item.TargetID == TargetID)
                {
                    filtered.Add(item);
                }
            }

            return filtered;
        }

    }

}

