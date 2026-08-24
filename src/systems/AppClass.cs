using Pariah_Cybersecurity;
using System.Text.Json.Nodes;
using static XRUIOS.Barebones.Interfaces.AppClass;
using static XRUIOS.Barebones.XRUIOS;
using File = System.IO.File;
using YuukoProtocol;


namespace XRUIOS.Barebones.Functions
{
    public static class AppClass
    {


        //Each app has an optional YuukoApp; it allows us to know what apps exist as an equivalent on other devices! Can be dev or user set

        public static XRUIOSAppManifest UpdateApp(XRUIOSAppManifest app, XRUIOSAppManifestPatch patch)
        {
            return new XRUIOSAppManifest(
                patch.AppId ?? app.AppId,
                patch.Name ?? app.Name,
                patch.Description ?? app.Description,
                patch.Author ?? app.Author,
                patch.Version ?? app.Version,
                patch.YuukoAppInfo ?? app.YuukoAppInfo,
                patch.EntryPoint ?? app.EntryPoint,
                app.Identifier // always keep original
            );
        }



        //C
        public static async Task AddApp(XRUIOSAppManifest App)
        {
            var directoryPath = Path.Combine(DataPath, "App");

            await DataHandler.JSONDataHandler.CreateJsonFile(App.Identifier, directoryPath, new JsonObject() { });


            var jsonFile = await DataHandler.JSONDataHandler.LoadJsonFile(App.Identifier, directoryPath);

            jsonFile = await DataHandler.JSONDataHandler.AddToJson<XRUIOSAppManifest>(jsonFile, "Data", App, encryptionKey);

            await DataHandler.JSONDataHandler.SaveJson(jsonFile);

        }
        //R
        public static async Task<List<XRUIOSAppManifest>> GetApp()
        {
            var basePath = Path.Combine(DataPath, "App");

            // Get all directories inside basePath, then just take the folder names
            var appIdentifiers = Directory.GetDirectories(basePath)
                            .Select(Path.GetFileName)
                            .ToList();

            var apps = new List<XRUIOSAppManifest>();

            foreach (var appIdentifier in appIdentifiers)
            {
                var directoryPath = Path.Combine(DataPath, "App");

                var jsonFile = await DataHandler.JSONDataHandler.LoadJsonFile(appIdentifier, directoryPath);

                var point = (XRUIOSAppManifest)await DataHandler.JSONDataHandler.GetVariable<XRUIOSAppManifest>(jsonFile, "Data", encryptionKey);
            }

            return apps;
        }


        public static async Task<XRUIOSAppManifest> GetApp(string identifier)
        {
            var directoryPath = Path.Combine(DataPath, "App");

            var filePath = Path.Combine(directoryPath, identifier + ".json");

            if (!File.Exists(filePath))
            {
                throw new InvalidOperationException("This App does not exist.");

            }

            var jsonFile = await DataHandler.JSONDataHandler.LoadJsonFile(identifier, directoryPath);

            var point = (XRUIOSAppManifest)await DataHandler.JSONDataHandler.GetVariable<XRUIOSAppManifest>(jsonFile, "Data", encryptionKey);

            return point;

        }


        //U
        public static async Task UpdateApp(XRUIOSAppManifest App)
        {
            var directoryPath = Path.Combine(DataPath, "App");
            var filePath = Path.Combine(directoryPath, App.Identifier + ".json");

            if (!File.Exists(filePath))
            {
                await DataHandler.JSONDataHandler.CreateJsonFile(
                    App.Identifier,
                    directoryPath,
                    new JsonObject()
                );
            }

            var jsonFile = await DataHandler.JSONDataHandler.LoadJsonFile(
                App.Identifier,
                directoryPath
            );

            jsonFile = await DataHandler.JSONDataHandler.UpdateJson<XRUIOSAppManifest>(
                jsonFile,
                "Data",
                App,
                encryptionKey
            );

            await DataHandler.JSONDataHandler.SaveJson(jsonFile);


        }
        //D
        public static void DeleteApp(string identifier)
        {
            var directoryPath = Path.Combine(DataPath, "App");

            var filePath = Path.Combine(directoryPath, identifier + ".json");

            if (!File.Exists(filePath))
                throw new InvalidOperationException("This App does not exist.");

            File.Delete(filePath); // only delete the app JSON file, leave folder intact
        }


    }

    public class AppFavoritesClass
    {

        //C
        public static async Task AddToFavorites(string appIdentifier, string directoryUUID)
        {
            var directoryPath = Path.Combine(DataPath, "App");

            var manager = new Bindings.DirectoryManager(directoryPath);

            await manager.LoadBindings();

            var favoritesFile = await DataHandler.JSONDataHandler.LoadJsonFile("AppFavorites", directoryPath);

            var favorites = (List<FileRecord>)await DataHandler.JSONDataHandler.GetVariable<List<FileRecord>>(favoritesFile, "Data", encryptionKey);
            //UUID, path name, path 


            if (!favorites.Any(d => d.UUID == directoryUUID & d.File == appIdentifier))
            {
                //Create new record

                var record = new FileRecord(directoryUUID, appIdentifier);

                favorites.Add(record);
            }

            else
            {
                throw new InvalidOperationException($"Song already favorited.");
            }


            var editedJSON = await DataHandler.JSONDataHandler.UpdateJson<List<FileRecord>>(favoritesFile, "Data", favorites, encryptionKey);

            await DataHandler.JSONDataHandler.SaveJson(editedJSON);

        }

        //R
        public static async Task<(List<string>, List<string>)> GetFavorites()
        {

            var directoryPath = Path.Combine(DataPath, "App");

            var manager = new Bindings.DirectoryManager(directoryPath);

            var favoritesFile = await DataHandler.JSONDataHandler.LoadJsonFile("AppFavorites", directoryPath);

            var favorites = (List<FileRecord>)await DataHandler.JSONDataHandler.GetVariable<List<FileRecord>>(favoritesFile, "Data", encryptionKey);
            //UUID, path name, path 

            //What bindings exist? Let's go through each and see

            List<string> resolvedFiles = new List<string>();
            List<string> unresolvedFiles = new List<string>();
            //UUID, Path Name, Path

            foreach (var file in favorites)
            {
                string? foundDirectoryPath = await manager.GetDirectoryById(file.UUID);

                if (!string.IsNullOrEmpty(foundDirectoryPath))
                {
                    resolvedFiles.Add(Path.Combine(foundDirectoryPath, file.File));
                }
                else
                {
                    unresolvedFiles.Add(file.File); 
                }
            }


            return (resolvedFiles, unresolvedFiles);

        }

        public static async Task<List<string>> GetFavoritePathsAsync(bool onlyResolved = true)
        {
            var (resolved, unresolved) = await GetFavorites();

            // Filter out any null or empty paths before returning
            resolved = resolved.Where(p => !string.IsNullOrEmpty(p)).ToList();
            unresolved = unresolved.Where(p => !string.IsNullOrEmpty(p)).ToList();

            if (onlyResolved)
            {
                return resolved;
            }

            var all = new List<string>(resolved);
            all.AddRange(unresolved);
            return all;
        }


        //D
        public static async Task RemoveFromFavorites(string appIdentifier, string directoryUUID)
        {
            var directoryPath = Path.Combine(DataPath, "App");
            var manager = new Bindings.DirectoryManager(directoryPath);
            await manager.LoadBindings();

            var favoritesFile = await DataHandler.JSONDataHandler.LoadJsonFile("AppFavorites", directoryPath);
            var favorites = (List<FileRecord>)await DataHandler.JSONDataHandler.GetVariable<List<FileRecord>>(favoritesFile, "Data", encryptionKey);
            var removedCount = favorites.RemoveAll(d => d.UUID == directoryUUID && d.File == appIdentifier);

            if (removedCount == 0)
            {
                Console.WriteLine($"Song '{appIdentifier}' (UUID: {directoryUUID}) was not in favorites.");
                return;
            }

            // Save updated list
            var editedJSON = await DataHandler.JSONDataHandler.UpdateJson<List<FileRecord>>(favoritesFile, "Data", favorites, encryptionKey);
            await DataHandler.JSONDataHandler.SaveJson(editedJSON);
        }
    }

}