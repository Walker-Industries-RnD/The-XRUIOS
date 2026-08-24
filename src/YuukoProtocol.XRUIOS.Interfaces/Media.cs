using Pariah_Cybersecurity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using WISecureData;

namespace YuukoProtocol
{
    public static class Media
    {




        public static async Task<ResolvedMedia> GetFile(string DataPath, string directoryUuid, string fileName, string DataType = "Generic", CancellationToken ct = default)
        {
            Bindings.DirectoryManager manager = new Bindings.DirectoryManager(Path.Combine(DataPath, DataType));

            await manager.LoadBindings();

            if (string.IsNullOrWhiteSpace(directoryUuid)) throw new ArgumentException("Invalid directory UUID.", nameof(directoryUuid));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("Invalid file name.", nameof(fileName));

            if (Path.IsPathRooted(fileName) || fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar))
                throw new InvalidOperationException("Invalid media file name.");

            var directory = await manager.GetDirectoryById(directoryUuid, ct: ct);
            if (directory == null) throw new DirectoryNotFoundException("Media directory could not be resolved.");

            var fullPath = Path.Combine(directory, fileName);
            var info = new FileInfo(fullPath);

            Console.WriteLine(fullPath);


            if (!info.Exists) throw new FileNotFoundException("Media file not found.", fileName);

            return new ResolvedMedia(info.FullName, info.Name, directoryUuid, info.Length, info.LastWriteTimeUtc);
        }

        public static async Task<DirectoryRecord?> GetOrCreateDirectory(string DataPath, SecureData encryptionKey, string fullFilePath, string? directory, string? directoryName, string DataType = "Generic", CancellationToken ct = default)
        {

            Bindings.DirectoryManager manager = new Bindings.DirectoryManager(Path.Combine(DataPath, DataType));

            await manager.LoadBindings();


            var (resolved, unresolved) = await GetGenericDirectories(DataPath, encryptionKey);
            var match = resolved.Concat(unresolved).FirstOrDefault(r => string.Equals(r.Path, directory, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            await AddGenericDirectory(DataPath, encryptionKey, directory!, directoryName ?? Guid.NewGuid().ToString());

            (resolved, unresolved) = await GetGenericDirectories(DataPath, encryptionKey);
            return resolved.Concat(unresolved).FirstOrDefault(r => string.Equals(r.Path, directory, StringComparison.OrdinalIgnoreCase));
        }

        public static async Task<DirectoryRecord> AddGenericDirectory(string DataPath, SecureData encryptionKey, string directory, string directoryName, string DataType = "Generic")
        {
            Bindings.DirectoryManager manager = new Bindings.DirectoryManager(Path.Combine(DataPath, DataType));

            await manager.LoadBindings();


            var bankFilePath = Path.Combine(DataPath, DataType, "GenericBank.json");

            if (!File.Exists(bankFilePath))
            {
                await DataHandler.JSONDataHandler.CreateJsonFile("GenericBank", Path.Combine(DataPath, DataType), new JsonObject());
                var json = await DataHandler.JSONDataHandler.LoadJsonFile("GenericBank", Path.Combine(DataPath, DataType));
                json = await DataHandler.JSONDataHandler.AddToJson<List<DirectoryRecord>>(json, "Data", new List<DirectoryRecord>(), encryptionKey);
                await DataHandler.JSONDataHandler.SaveJson(json);
            }

            var directoryFile = await DataHandler.JSONDataHandler.LoadJsonFile("GenericBank", Path.Combine(DataPath, DataType));
            var directories = (List<DirectoryRecord>)await DataHandler.JSONDataHandler.GetVariable<List<DirectoryRecord>>(directoryFile, "Data", encryptionKey);

            var (uuid, resolvedPath) = await manager.GetOrCreateDirectory(directory);

            var existing = directories.FirstOrDefault(d => d.UUID == uuid);
            if (existing != null) return existing;

            var newRecord = new DirectoryRecord(uuid, directoryName, resolvedPath);
            directories.Add(newRecord);

            var editedJSON = await DataHandler.JSONDataHandler.UpdateJson<List<DirectoryRecord>>(directoryFile, "Data", directories, encryptionKey);
            await DataHandler.JSONDataHandler.SaveJson(editedJSON);

            return newRecord;
        }

        public static async Task<(List<DirectoryRecord>, List<DirectoryRecord>)> GetGenericDirectories(string DataPath, SecureData encryptionKey, string DataType = "Generic")
        {
            Bindings.DirectoryManager manager = new Bindings.DirectoryManager(Path.Combine(DataPath, DataType));

            await manager.LoadBindings();


            var directoryFile = await DataHandler.JSONDataHandler.LoadJsonFile("GenericBank", Path.Combine(DataPath, DataType));
            var directories = (List<DirectoryRecord>)await DataHandler.JSONDataHandler.GetVariable<List<DirectoryRecord>>(directoryFile, "Data", encryptionKey);

            var resolved = new List<DirectoryRecord>();
            var unresolved = new List<DirectoryRecord>();

            foreach (var dir in directories)
            {
                var path = await manager.GetDirectoryById(dir.UUID);
                if (path != null)
                    resolved.Add(dir with { Path = path });
                else
                    unresolved.Add(dir);
            }

            return (resolved, unresolved);
        }

        public static async Task UpdateGenericDirectory(string DataPath, SecureData encryptionKey, string uuid, string newDirectory, string newDirectoryName, string DataType = "Generic")
        {
            Bindings.DirectoryManager manager = new Bindings.DirectoryManager(Path.Combine(DataPath, DataType));

            await manager.LoadBindings();

            var directoryFile = await DataHandler.JSONDataHandler.LoadJsonFile("GenericBank", Path.Combine(DataPath, DataType));
            var directories = (List<DirectoryRecord>)await DataHandler.JSONDataHandler.GetVariable<List<DirectoryRecord>>(directoryFile, "Data", encryptionKey);

            var recordIndex = directories.FindIndex(d => d.UUID == uuid);
            if (recordIndex == -1)
            {
                Console.WriteLine($"No record found for UUID {uuid}.");
                return;
            }

            directories[recordIndex] = directories[recordIndex] with
            {
                Path = newDirectory,
                PathName = newDirectoryName
            };

            var newResolution = new Bindings.DirectoryResolution(newDirectory, verified: true);
            var updateResult = await manager.UpdateBinding(uuid, newResolution);
            if (!updateResult)
            {

                await manager.GetOrCreateDirectory(newDirectory);
                await manager.UpdateBinding(uuid, newResolution);
            }

            var editedJSON = await DataHandler.JSONDataHandler.UpdateJson<List<DirectoryRecord>>(directoryFile, "Data", directories, encryptionKey);
            await DataHandler.JSONDataHandler.SaveJson(editedJSON);
        }



        public static async Task RemoveGenericDirectory(string DataPath, SecureData encryptionKey, string uuid, string DataType = "Generic", bool deleteDirectory = true)
        {
            Bindings.DirectoryManager manager = new Bindings.DirectoryManager(Path.Combine(DataPath, DataType));

            await manager.LoadBindings();


            var directoryFile = await DataHandler.JSONDataHandler.LoadJsonFile("GenericBank", Path.Combine(DataPath, DataType));
            var directories = (List<DirectoryRecord>)await DataHandler.JSONDataHandler.GetVariable<List<DirectoryRecord>>(directoryFile, "Data", encryptionKey);

            var record = directories.FirstOrDefault(d => d.UUID == uuid);
            if (record == null)
            {
                Console.WriteLine($"No record found for UUID {uuid}.");
                return;
            }

            // Get path FIRST
            string? currentPath = await manager.GetDirectoryById(uuid);

            if (deleteDirectory && currentPath != null && Directory.Exists(currentPath))
            {
                Directory.Delete(currentPath, true);
            }

            // Now remove binding
            await manager.DeleteBinding(uuid);

            // Remove JSON record
            directories.Remove(record);
            var editedJSON = await DataHandler.JSONDataHandler.UpdateJson<List<DirectoryRecord>>(directoryFile, "Data", directories, encryptionKey);
            await DataHandler.JSONDataHandler.SaveJson(editedJSON);
        }

    }

}
