using Pariah_Cybersecurity;
using System.Text.Json.Nodes;
using static XRUIOS.Barebones.Interfaces.MediaAlbumClass;
using static XRUIOS.Barebones.XRUIOS;
using YuukoProtocol;

namespace XRUIOS.Barebones.Functions
{
    public static class MediaAlbumClass
    {
    

        public static async Task AddMediaAlbum(AlbumMedia MediaAlbum)
        {
            var directoryPath = Path.Combine(DataPath, "MediaAlbum");
            Directory.CreateDirectory(directoryPath);

            var filePath = Path.Combine(directoryPath, MediaAlbum.Identifier + ".json");

            if (File.Exists(filePath))
            {
                throw new InvalidOperationException($"MediaAlbum with ID '{MediaAlbum.Identifier}' already exists.");
            }

            await DataHandler.JSONDataHandler.CreateJsonFile(MediaAlbum.Identifier, directoryPath, new JsonObject() { });

            var jsonFile = await DataHandler.JSONDataHandler.LoadJsonFile(MediaAlbum.Identifier, directoryPath);

            jsonFile = await DataHandler.JSONDataHandler.AddToJson<AlbumMedia>(jsonFile, "Data", MediaAlbum, encryptionKey);

            await DataHandler.JSONDataHandler.SaveJson(jsonFile);
        }

        public static async Task<List<AlbumMedia>> GetMediaAlbums()
        {
            var basePath = Path.Combine(DataPath, "MediaAlbum");

            if (!Directory.Exists(basePath))
                return new List<AlbumMedia>();

            var files = Directory.GetFiles(basePath, "*.json");

            var MediaAlbums = new List<AlbumMedia>();

            foreach (var file in files)
            {
                var identifier = Path.GetFileNameWithoutExtension(file);

                try
                {
                    var jsonFile = await DataHandler.JSONDataHandler.LoadJsonFile(identifier, basePath);
                    var obj = await DataHandler.JSONDataHandler.GetVariable<object>(jsonFile, "Data", encryptionKey);

                    if (obj is AlbumMedia album)
                    {
                        MediaAlbums.Add(album);
                    }
                    else
                    {
                        // skip corrupted/non-album data
                        continue;
                    }
                }
                catch
                {
                    // skip unreadable/corrupted files
                    continue;
                }
            }

            return MediaAlbums;
        }

        public static async Task<AlbumMedia> GetMediaAlbum(string identifier)
        {
            var directoryPath = Path.Combine(DataPath, "MediaAlbum");

            var filePath = Path.Combine(directoryPath, identifier + ".json");

            if (!File.Exists(filePath))
            {
                throw new InvalidOperationException("This MediaAlbum does not exist.");
            }

            var jsonFile = await DataHandler.JSONDataHandler.LoadJsonFile(identifier, directoryPath);

            var obj = await DataHandler.JSONDataHandler.GetVariable<object>(jsonFile, "Data", encryptionKey);

            if (obj is AlbumMedia album)
                return album;

            throw new InvalidOperationException("MediaAlbum data is not in the expected format.");
        }

        public static async Task UpdateMediaAlbum(AlbumMedia MediaAlbum)
        {
            var directoryPath = Path.Combine(DataPath, "MediaAlbum");
            var filePath = Path.Combine(directoryPath, MediaAlbum.Identifier + ".json");

            if (!File.Exists(filePath))
                throw new InvalidOperationException($"MediaAlbum with ID '{MediaAlbum.Identifier}' doesn't exist.");

            var jsonFile = await DataHandler.JSONDataHandler.LoadJsonFile(MediaAlbum.Identifier, directoryPath);

            jsonFile = await DataHandler.JSONDataHandler.UpdateJson<AlbumMedia>(jsonFile, "Data", MediaAlbum, encryptionKey);

            await DataHandler.JSONDataHandler.SaveJson(jsonFile);
        }

        public static void DeleteMediaAlbum(string identifier)
        {
            var filePath = Path.Combine(DataPath, "MediaAlbum", identifier + ".json");

            if (!File.Exists(filePath))
                throw new InvalidOperationException("This MediaAlbum does not exist.");

            File.Delete(filePath);
        }
    }

    public class MediaAlbumFavoritesClass
    {
        public static async Task AddToFavorites(string MediaAlbumIdentifier, string directoryUUID)
        {
            var directoryPath = Path.Combine(DataPath, "MediaAlbum");
            Directory.CreateDirectory(directoryPath);

            var manager = new Bindings.DirectoryManager(directoryPath);
            await manager.LoadBindings();

            var favoritesFile = await DataHandler.JSONDataHandler.LoadJsonFile("MediaAlbumFavorites", directoryPath);

            List<FileRecord> favorites;
            try
            {
                favorites = (List<FileRecord>)await DataHandler.JSONDataHandler.GetVariable<List<FileRecord>>(favoritesFile, "Data", encryptionKey);
            }
            catch
            {
                favorites = new List<FileRecord>();
            }

            if (!favorites.Any(d => d.UUID == directoryUUID && d.File == MediaAlbumIdentifier))
            {
                var record = new FileRecord(directoryUUID, MediaAlbumIdentifier);
                favorites.Add(record);
            }
            else
            {
                throw new InvalidOperationException($"Song already favorited.");
            }

            var editedJSON = await DataHandler.JSONDataHandler.UpdateJson<List<FileRecord>>(favoritesFile, "Data", favorites, encryptionKey);
            await DataHandler.JSONDataHandler.SaveJson(editedJSON);
        }

        public static async Task<(List<string>, List<string>)> GetFavorites()
        {
            var directoryPath = Path.Combine(DataPath, "MediaAlbum");
            Directory.CreateDirectory(directoryPath);

            var manager = new Bindings.DirectoryManager(directoryPath);
            await manager.LoadBindings();

            var favoritesFile = await DataHandler.JSONDataHandler.LoadJsonFile("MediaAlbumFavorites", directoryPath);

            List<FileRecord> favorites;
            try
            {
                favorites = (List<FileRecord>)await DataHandler.JSONDataHandler.GetVariable<List<FileRecord>>(favoritesFile, "Data", encryptionKey);
            }
            catch
            {
                return (new List<string>(), new List<string>());
            }

            List<string> resolvedFiles = new List<string>();
            List<string> unresolvedFiles = new List<string>();

            foreach (var file in favorites)
            {
                string? foundDirectoryPath = await manager.GetDirectoryById(file.UUID);
                if (string.IsNullOrEmpty(foundDirectoryPath))
                {
                    unresolvedFiles.Add(file.File);
                    continue;
                }

                var resolvedPath = Path.Combine(foundDirectoryPath, file.File + ".json");

                if (File.Exists(resolvedPath))
                    resolvedFiles.Add(resolvedPath);
                else
                    unresolvedFiles.Add(file.File);
            }

            return (resolvedFiles, unresolvedFiles);
        }

        public static async Task<List<string>> GetFavoritePathsAsync(bool onlyResolved = true)
        {
            var (resolved, unresolved) = await GetFavorites();

            if (onlyResolved)
                return resolved;

            var all = new List<string>(resolved);
            all.AddRange(unresolved);
            return all;
        }

        public static async Task RemoveFromFavorites(string MediaAlbumIdentifier, string directoryUUID)
        {
            var directoryPath = Path.Combine(DataPath, "MediaAlbum");
            Directory.CreateDirectory(directoryPath);

            var manager = new Bindings.DirectoryManager(directoryPath);
            await manager.LoadBindings();

            var favoritesFile = await DataHandler.JSONDataHandler.LoadJsonFile("MediaAlbumFavorites", directoryPath);

            List<FileRecord> favorites;
            try
            {
                favorites = (List<FileRecord>)await DataHandler.JSONDataHandler.GetVariable<List<FileRecord>>(favoritesFile, "Data", encryptionKey);
            }
            catch
            {
                throw new InvalidOperationException($"MediaAlbum favorites file is corrupted or missing.");
            }

            var removedCount = favorites.RemoveAll(d => d.UUID == directoryUUID && d.File == MediaAlbumIdentifier);

            if (removedCount == 0)
            {
                Console.WriteLine($"Song '{MediaAlbumIdentifier}' (UUID: {directoryUUID}) was not in favorites.");
                return;
            }

            var editedJSON = await DataHandler.JSONDataHandler.UpdateJson<List<FileRecord>>(favoritesFile, "Data", favorites, encryptionKey);
            await DataHandler.JSONDataHandler.SaveJson(editedJSON);
        }
    }
}
