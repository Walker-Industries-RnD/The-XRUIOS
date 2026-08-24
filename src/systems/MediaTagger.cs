using Pariah_Cybersecurity;
using System.Text.Json.Nodes;
using static XRUIOS.Barebones.Interfaces.CreatorClass;
using static XRUIOS.Barebones.XRUIOS;
using YuukoProtocol;

namespace XRUIOS.Barebones.Functions
{
    internal class MediaTagger
    {

        //Was specfically made for Images and Videos but can be used for anything, uses CRUD format
        //This is actually taken from CreatorClass, which had a lot of what we needed!

        public class CreatorClass
        {


            public class CreatorFileClass
            {
                //C

                //IMPORTANT, PFP and Files use Media, not Yuuko Bindings (Although they're connected)

                public static async Task CreateCreator(string CreatorName, string? Description, string? PFPPath, List<string> FilePaths, string CreatorType)
                {

                    var directoryPath = Path.Combine(DataPath, "Creators", CreatorType);

                    var manager = new Bindings.DirectoryManager(directoryPath);

                    var folder = Path.Combine(DataPath, "Creators", CreatorType);
                    var fileNameToFind = CreatorName;

                    var foundFile = Directory.EnumerateFiles(folder, fileNameToFind + ".*").FirstOrDefault();

                    if (foundFile != null)
                    {
                        Console.WriteLine($"Creator already exists.");
                        return;
                    }

                    //Check if file exists 

                    var fileExists = File.Exists(directoryPath);

                    //No? Let's continue

                    await manager.LoadBindings();


                    if (Description == null)
                    {
                        Description = "No Description Provided.";
                    }

                    FileRecord PossiblePFP = null;

                    if (PFPPath != null)
                    {

                        var fileDirectoryID = await Media.GetOrCreateDirectory(DataPath, encryptionKey, PFPPath, Path.GetDirectoryName(PFPPath), Guid.NewGuid().ToString());

                        var fileName = Path.GetFileName(PFPPath);

                        var newFileRecord = new FileRecord(fileDirectoryID.UUID, fileName);

                        PossiblePFP = newFileRecord;


                    }

                    List<FileRecord> Files = new List<FileRecord>();

                    foreach (var file in FilePaths)
                    {
                        var fileDirectoryID = await Media.GetOrCreateDirectory(DataPath, encryptionKey, file, Path.GetDirectoryName(file), Guid.NewGuid().ToString());
                        var fileName = Path.GetFileName(file);

                        Files.Add(new FileRecord(fileDirectoryID.UUID, fileName));
                    }


                    var newCreator = new Creator(CreatorName, Description, PossiblePFP, Files);

                    //And now we save

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
                        var fileDirectoryID = await Media.GetOrCreateDirectory(DataPath, encryptionKey, file, Path.GetDirectoryName(file), Guid.NewGuid().ToString());
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

                    var directoryPath = Path.Combine(DataPath, "Music");

                    var manager = new Bindings.DirectoryManager(directoryPath);

                    var favoritesFile = await DataHandler.JSONDataHandler.LoadJsonFile("CreatorFavorites", directoryPath);

                    var favorites = (List<string>)await DataHandler.JSONDataHandler.GetVariable<List<string>>(favoritesFile, "Data", encryptionKey);
                    //UUID, path name, path 

                    //What bindings exist? Let's go through each and see

                    List<string> resolvedFiles = new List<string>();
                    List<string> unresolvedFiles = new List<string>();
                    //UUID, Path Name, Path

                    foreach (var item in favorites)
                    {
                        var creatorData = await CreatorFileClass.GetCreator(item, CreatorType);

                        if (creatorData == null)
                        {
                            unresolvedFiles.Add(item);
                        }

                        else
                        {
                            resolvedFiles.Add(item);

                        }
                    }

                    return (resolvedFiles, unresolvedFiles);

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


        }



    }
}
