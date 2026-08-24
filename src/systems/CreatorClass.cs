using Pariah_Cybersecurity;
using System.Text.Json.Nodes;
using static Pariah_Cybersecurity.DataHandler;
using static XRUIOS.Barebones.XRUIOS;
using YuukoProtocol;

namespace XRUIOS.Barebones
{


    //Originally made for music, it's pretty expandable! 
    public class CreatorClass
    {
        public record Creator
        {
            public string Name;
            public string Description;
            public FileRecord? PFP;
            public List<FileRecord> Files;

            public Creator() { }

            public Creator(string name, string description, FileRecord? pfp, List<FileRecord?> files)
            {
                this.Name = name;
                this.Description = description;
                this.PFP = pfp;
                this.Files = files;
            }

        }

        public class CreatorFileClass
        {
            //C

            //IMPORTANT, PFP and Files use Media, not Yuuko Bindings (Although they're connected)

            public static async Task CreateCreator(string CreatorName, string? Description, string? PFPPath, List<string> FilePaths, string CreatorType)
            {
                var directoryPath = Path.Combine(DataPath, "Creators", CreatorType);

                await InitiateCreatorClass(CreatorType);


                var manager = new Bindings.DirectoryManager(directoryPath);

                var folder = Path.Combine(DataPath, "Creators", CreatorType);

                // Ensure the directory exists
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var fileNameToFind = CreatorName;

                var foundFile = Directory.EnumerateFiles(folder, fileNameToFind + ".*").FirstOrDefault();

                if (foundFile != null)
                {
                    Console.WriteLine($"Creator already exists.");
                    return;
                }

                await manager.LoadBindings();

                if (Description == null)
                {
                    Description = "No Description Provided.";
                }

                FileRecord PossiblePFP = null;

                if (PFPPath != null)
                {
                    var fileDirectoryID = await Media.GetOrCreateDirectory(DataPath, encryptionKey, PFPPath, Path.GetDirectoryName(PFPPath), Guid.NewGuid().ToString(), DataType: "Creators");
                    var fileName = Path.GetFileName(PFPPath);
                    PossiblePFP = new FileRecord(fileDirectoryID.UUID, fileName);
                }

                List<FileRecord> Files = new List<FileRecord>();

                foreach (var file in FilePaths)
                {
                    var fileDirectoryID = await Media.GetOrCreateDirectory(DataPath, encryptionKey, file, Path.GetDirectoryName(file), Guid.NewGuid().ToString(), DataType: "Creators");
                    var fileName = Path.GetFileName(file);
                    Files.Add(new FileRecord(fileDirectoryID.UUID, fileName));
                }

                var newCreator = new Creator(CreatorName, Description, PossiblePFP, Files);

                await DataHandler.JSONDataHandler.CreateJsonFile(CreatorName, directoryPath, new JsonObject());

                var creatorFile = await DataHandler.JSONDataHandler.LoadJsonFile(CreatorName, directoryPath);

                var editedJSON = await DataHandler.JSONDataHandler.AddToJson<Creator>(creatorFile, "Data", newCreator, encryptionKey);

                await DataHandler.JSONDataHandler.SaveJson(editedJSON);
            }

            //R
            public static async Task<Creator> GetCreator(string CreatorName, string CreatorType)
            {

                var directoryPath = Path.Combine(DataPath, "Creators", CreatorType);

                var folder = Path.Combine(DataPath, "Creators", CreatorType);
                var fileNameToFind = CreatorName;

                var foundFile = Directory.EnumerateFiles(folder, fileNameToFind + ".*").FirstOrDefault();

                if (foundFile == null)
                {
                    Console.WriteLine($"Creator doesn't exist.");
                    return null;
                }

                var creatorFile = await DataHandler.JSONDataHandler.LoadJsonFile(CreatorName, directoryPath);

                var CreatorData = (Creator)await DataHandler.JSONDataHandler.GetVariable<Creator>(creatorFile, "Data", encryptionKey);

                return CreatorData;

            }

            public static async Task<(string, string)> GetCreatorOverview(string CreatorName, string CreatorType)
            {

                var directoryPath = Path.Combine(DataPath, "Creators", CreatorType);

                var folder = Path.Combine(DataPath, "Creators", CreatorType);
                var fileNameToFind = CreatorName;

                var foundFile = Directory.EnumerateFiles(folder, fileNameToFind + ".*").FirstOrDefault();

                if (foundFile == null)
                {
                    Console.WriteLine($"Creator doesn't exist.");
                    return (null, null);
                }

                var creatorFile = await DataHandler.JSONDataHandler.LoadJsonFile(CreatorName, directoryPath);

                var CreatorData = (Creator)await DataHandler.JSONDataHandler.GetVariable<Creator>(creatorFile, "Data", encryptionKey);

                return (CreatorData.Name, CreatorData.Description);

            }

            public static async Task<List<FileRecord>> GetCreatorFiles(string CreatorName, string CreatorType)
            {

                var directoryPath = Path.Combine(DataPath, "Creators", CreatorType);

                var folder = Path.Combine(DataPath, "Creators", CreatorType);
                var fileNameToFind = CreatorName;

                var foundFile = Directory.EnumerateFiles(folder, fileNameToFind + ".*").FirstOrDefault();

                if (foundFile == null)
                {
                    Console.WriteLine($"Creator doesn't exist.");
                    return null;
                }

                var creatorFile = await DataHandler.JSONDataHandler.LoadJsonFile(CreatorName, directoryPath);

                var CreatorData = (Creator)await DataHandler.JSONDataHandler.GetVariable<Creator>(creatorFile, "Data", encryptionKey);

                return (CreatorData.Files);

            }

            //U
            //(You can't edit the name)

            public static async Task AddFile(string CreatorName, string CreatorType, List<string> FilePaths)
            {
                var CreatorFile = await GetCreator(CreatorName, CreatorType);
                if (CreatorFile == null)
                    throw new InvalidOperationException("Creator not found.");
                foreach (var file in FilePaths)
                {
                    var fileDirectoryID = await Media.GetOrCreateDirectory(DataPath, encryptionKey, file, Path.GetDirectoryName(file), Guid.NewGuid().ToString(), DataType: "Creators");
                    var fileName = Path.GetFileName(file);

                    CreatorFile.Files.Add(new FileRecord(fileDirectoryID.UUID, fileName));
                }

                var directoryPath = Path.Combine(DataPath, "Creators", CreatorType);

                var CreatorJSON = await DataHandler.JSONDataHandler.LoadJsonFile(CreatorName, directoryPath);

                var CreatorData = await DataHandler.JSONDataHandler.UpdateJson<Creator>(CreatorJSON, "Data", CreatorFile, encryptionKey);

                await DataHandler.JSONDataHandler.SaveJson(CreatorData);

            }

            public static async Task SetDescription(string CreatorName, string CreatorType, string Description)
            {
                var CreatorFile = await GetCreator(CreatorName, CreatorType);
                if (CreatorFile == null)
                    throw new InvalidOperationException("Creator not found.");

                CreatorFile.Description = Description;

                var directoryPath = Path.Combine(DataPath, "Creators", CreatorType);

                var CreatorJSON = await DataHandler.JSONDataHandler.LoadJsonFile(CreatorName, directoryPath);

                var CreatorData = await DataHandler.JSONDataHandler.UpdateJson<Creator>(CreatorJSON, "Data", CreatorFile, encryptionKey);

                await DataHandler.JSONDataHandler.SaveJson(CreatorData);

            }



            //D
            public static async Task RemoveFiles(string CreatorName, string CreatorType, List<FileRecord> filesToRemove)
            {
                var creator = await GetCreator(CreatorName, CreatorType);
                if (creator == null)
                    throw new InvalidOperationException("Creator not found.");

                var directoryPath = Path.Combine(DataPath, "Creators", CreatorType);

                foreach (var item in filesToRemove)
                {
                    var fileRecord = creator.Files.FirstOrDefault(f => f.File == item.File && f.UUID == item.UUID);
                    if (fileRecord != null)
                    {
                        creator.Files.Remove(fileRecord);
                    }
                    else
                    {
                        Console.WriteLine($"Warning: File '{item.File}' with UUID '{item.UUID}' not found in creator '{CreatorName}'.");
                    }
                }

                var CreatorJSON = await DataHandler.JSONDataHandler.LoadJsonFile(CreatorName, directoryPath);
                var editedJSON = await DataHandler.JSONDataHandler.UpdateJson<Creator>(CreatorJSON, "Data", creator, encryptionKey);
                await DataHandler.JSONDataHandler.SaveJson(editedJSON);
            }
        }

        public class CreatorFavoritesClass
        {


            //C
            public static async Task AddToFavorites(string CreatorName, string CreatorType)
            {
                var directoryPath = Path.Combine(DataPath, "Creators", CreatorType);

                var folder = Path.Combine(DataPath, "Creators", CreatorType);
                var fileNameToFind = CreatorName;

                var foundFile = Directory.EnumerateFiles(folder, fileNameToFind + ".*").FirstOrDefault();

                if (foundFile == null)
                {
                    Console.WriteLine($"Creator doesn't exist.");
                    return;
                }
                var manager = new Bindings.DirectoryManager(directoryPath);

                await manager.LoadBindings();

                var favoritesFile = await DataHandler.JSONDataHandler.LoadJsonFile("CreatorFavorites", directoryPath);

                var favorites = (List<string>)await DataHandler.JSONDataHandler.GetVariable<List<string>>(favoritesFile, "Data", encryptionKey);
                //UUID, path name, path 


                if (!favorites.Any(d => d == CreatorName))
                {
                    favorites.Add(CreatorName);
                }

                else
                {
                    throw new InvalidOperationException($"Creator already favorited.");
                }


                var editedJSON = await DataHandler.JSONDataHandler.UpdateJson<List<string>>(favoritesFile, "Data", favorites, encryptionKey);

                await DataHandler.JSONDataHandler.SaveJson(editedJSON);

            }

            //R
            public static async Task<(List<string>, List<string>)> GetFavorites(string CreatorType)
            {
                var directoryPath = Path.Combine(DataPath, "Creators", CreatorType);
                var manager = new Bindings.DirectoryManager(directoryPath); // probably not even needed here

                var favoritesFile = await DataHandler.JSONDataHandler.LoadJsonFile("CreatorFavorites", directoryPath);


                var favorites = (List<string>) await DataHandler.JSONDataHandler.GetVariable<List<string>>(favoritesFile, "Data", encryptionKey);

                var resolvedPaths = new List<string>();
                var unresolvedNames = new List<string>();

                foreach (var creatorName in favorites)
                {
                    var jsonPath = Path.Combine(directoryPath, $"{creatorName}.json");  

                    if (File.Exists(jsonPath))
                    {
                        resolvedPaths.Add(jsonPath);  
                                                      
                    }
                    else
                    {
                        unresolvedNames.Add(creatorName);
                    }
                }

                return (resolvedPaths, unresolvedNames);
            }
            public static async Task<List<string>> GetFavoritePathsAsync(string CreatorType, bool onlyResolved = true)
            {
                var (resolved, unresolved) = await GetFavorites(CreatorType);

                if (onlyResolved)
                {
                    return resolved;
                }

                var all = new List<string>(resolved);
                all.AddRange(unresolved);
                return all;
            }

            //D
            public static async Task RemoveFromFavorites(string CreatorName, string CreatorType)
            {
                var directoryPath = Path.Combine(DataPath, "Creators", CreatorType);

                var folder = Path.Combine(DataPath, "Creators", CreatorType);
                var fileNameToFind = CreatorName;

                var foundFile = Directory.EnumerateFiles(folder, fileNameToFind + ".*").FirstOrDefault();

                if (foundFile == null)
                {
                    Console.WriteLine($"Creator doesn't exist.");
                    return;
                }
                var manager = new Bindings.DirectoryManager(directoryPath);

                await manager.LoadBindings();

                var favoritesFile = await DataHandler.JSONDataHandler.LoadJsonFile("CreatorFavorites", directoryPath);

                var favorites = (List<string>)await DataHandler.JSONDataHandler.GetVariable<List<string>>(favoritesFile, "Data", encryptionKey);
                //UUID, path name, path 


                if (favorites.Any(d => d == CreatorName))
                {
                    favorites.Remove(CreatorName);
                }

                else
                {
                    throw new InvalidOperationException($"Creator not favorited.");
                }


                var editedJSON = await DataHandler.JSONDataHandler.UpdateJson<List<string>>(favoritesFile, "Data", favorites, encryptionKey);

                await DataHandler.JSONDataHandler.SaveJson(editedJSON);

            }
        }


        //Create a tag system later
        //Dictionary CreatorName, List<string> tags

        //Auto init when you create qa new creator class
        public static async Task InitiateCreatorClass(string CreatorType)
        {
            var directoryPath = Path.Combine(DataPath, "Creators", CreatorType); 
            Directory.CreateDirectory(directoryPath);

            var filePath = Path.Combine(directoryPath, "CreatorFavorites.json");

            if (!File.Exists(filePath))
            {
                var manager = new Bindings.DirectoryManager(directoryPath);

                await JSONDataHandler.CreateJsonFile("CreatorFavorites", directoryPath, new JsonObject());

                var favoritesFile = await JSONDataHandler.LoadJsonFile("CreatorFavorites", directoryPath);
                favoritesFile = await JSONDataHandler.AddToJson<List<string>>(favoritesFile, "Data", new List<string>(), encryptionKey);

                await JSONDataHandler.SaveJson(favoritesFile);
            }
        }






    }

}
