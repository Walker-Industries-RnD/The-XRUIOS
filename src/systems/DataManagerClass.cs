using Pariah_Cybersecurity;
using System.Text.Json.Nodes;
using static Pariah_Cybersecurity.DataHandler;
using static XRUIOS.Barebones.XRUIOS;
using YuukoProtocol;

namespace XRUIOS.Barebones.Functions
{
    public static class DataManagerClass
    {

        //Worldpoints are data handling all things from console to 2D to 3D

        //Dataslots are a chunk of reality, 2D or 3D, filled with tons of Worldpoints by GUID

        //Sessions ARE reality, created by connecting dataslots by GUID


        //Example; we want to run a "Work" session, which will fill stuff at home and a friends house
        //You can go to these places and switch the specfic stuff happening VIA dataslot

        public static class SessionClass
        {

            public record Session
            {
                public DateTime Created; //The date and time created, UtcNow
                public string Title; //Title
                public string Description; //Description
                public List<string> WorldPointIdentifiers;
                public string Identifier;

                //Logo exists in the same path

                public Session() { }


                public Session(DateTime? CreatedDateTime, string Title, string Description, List<string> identifiers, string? identifier)
                {
                    if (CreatedDateTime == null)
                    {
                        CreatedDateTime = DateTime.Now;
                    }

                    else
                    {
                        this.Created = (DateTime)CreatedDateTime;
                    }

                    this.Title = Title;
                    this.Description = Description;
                    this.WorldPointIdentifiers = identifiers;

                    Identifier = identifier ?? Guid.NewGuid().ToString();

                }
            }



            //C
            public static async Task AddSession(Session Session)
            {
                var directoryPath = Path.Combine(DataPath, "Session", Session.Identifier);

                await InitiateSession(Session.Identifier);

                var jsonFile = await DataHandler.JSONDataHandler.LoadJsonFile(Session.Identifier, directoryPath);

                jsonFile = await DataHandler.JSONDataHandler.UpdateJson<Session>(jsonFile, "Data", Session, encryptionKey);

                await DataHandler.JSONDataHandler.SaveJson(jsonFile);

            }
            //R
            public static List<string> GetSession()
            {
                var basePath = Path.Combine(DataPath, "Session");

                // Get all directories inside basePath, then just take the folder names
                return Directory.GetDirectories(basePath)
                                .Select(Path.GetFileName)
                                .ToList();
            }


            public static async Task<Session> GetSession(string identifier)
            {
                var directoryPath = Path.Combine(DataPath, "Session", identifier);

                var filePath = Path.Combine(directoryPath, identifier + ".json");

                if (!File.Exists(filePath))
                {
                    throw new InvalidOperationException("This Session does not exist.");

                }

                var jsonFile = await DataHandler.JSONDataHandler.LoadJsonFile(identifier, directoryPath);

                var point = (Session)await DataHandler.JSONDataHandler.GetVariable<Session>(jsonFile, "Data", encryptionKey);

                return point;

            }

            //U
            public static async Task UpdateSession(Session Session)
            {
                var directoryPath = Path.Combine(DataPath, "Session", Session.Identifier);

                await DataHandler.JSONDataHandler.CreateJsonFile(Session.Identifier, directoryPath, new JsonObject() { });


                var jsonFile = await DataHandler.JSONDataHandler.LoadJsonFile(Session.Identifier, directoryPath);

                jsonFile = await DataHandler.JSONDataHandler.UpdateJson<Session>(jsonFile, "Data", Session, encryptionKey);

                await DataHandler.JSONDataHandler.SaveJson(jsonFile);

            }
            //D
            public static void DeleteSession(string identifier)
            {
                var directoryPath = Path.Combine(DataPath, "Session", identifier);

                if (!Directory.Exists(directoryPath))
                    throw new InvalidOperationException("This Session does not exist.");

                Directory.Delete(directoryPath, true); // 'true' = delete everything inside
            }



            //Update method
            public record SessionPatch
            {
                public DateTime? Created { get; init; }             // Optional: only update if not null
                public string? Title { get; init; }                 // Optional
                public string? Description { get; init; }           // Optional
                public List<string>? WorldPointIdentifiers { get; init; }  // Optional
            }

            public static Session UpdateSession(Session session, SessionPatch patch)
            {
                return new Session(
                    patch.Created ?? session.Created,
                    patch.Title ?? session.Title,
                    patch.Description ?? session.Description,
                    patch.WorldPointIdentifiers ?? session.WorldPointIdentifiers,
                    session.Identifier // always keep original
                );
            }


            public static async Task InitiateSession(string identifier)
            {
                var basePath = Path.Combine(DataPath, "Session");
                Directory.CreateDirectory(basePath);

                var sessionPath = Path.Combine(basePath, identifier);
                Directory.CreateDirectory(sessionPath);

                var filePath = Path.Combine(sessionPath, identifier + ".json");

                if (!File.Exists(filePath))
                {
                    await JSONDataHandler.CreateJsonFile(identifier, sessionPath, new JsonObject());

                    var jsonFile = await JSONDataHandler.LoadJsonFile(identifier, sessionPath);
                    jsonFile = await JSONDataHandler.AddToJson<DataManagerClass.SessionClass.Session>(
                        jsonFile,
                        "Data",
                        new DataManagerClass.SessionClass.Session(),
                        encryptionKey
                    );

                    await JSONDataHandler.SaveJson(jsonFile);
                }
            }






        }


        public static class DataSlotClass
        {

            public record DataSlot
            {
                public bool IsFavorite; //If this is favorited
                public DateTime DateAndTime; //The date and time it was made
                public string Title; //Title
                public string Description; //Description
                public FileRecord ImgPath; //The path to the img icon
                public FileRecord TextureFolder; //2.5D images for previewing, for v2
                public List<string> Sessions; //GUIDs
                public string Identifier;

                public DataSlot() { }
                public DataSlot(
                    bool isFavorite,
                    DateTime? dateTimeVar, // nullable, may be default
                    string title,
                    string description,
                    FileRecord imgPath,
                    FileRecord? textureFolder, // optional for now
                    List<string> structSessions,
                    string? identifier)
                {
                    IsFavorite = isFavorite;

                    this.DateAndTime = dateTimeVar ?? DateTime.UtcNow;

                    Title = title;
                    Description = description;
                    ImgPath = imgPath;
                    TextureFolder = textureFolder ?? default; // still allows null/default
                    Sessions = structSessions ?? new List<string>();
                    Identifier = identifier ?? Guid.NewGuid().ToString();

                }

            }

            public record DataSlotPatch
            {
                public bool? IsFavorite { get; init; }
                public DateTime? DateAndTime { get; init; }
                public string? Title { get; init; }
                public string? Description { get; init; }
                public FileRecord? ImgPath { get; init; }
                public FileRecord? TextureFolder { get; init; }
                public List<string>? Sessions { get; init; }
            }

            public static DataSlot UpdateDataSlot(DataSlot slot, DataSlotPatch patch)
            {
                return new DataSlot(
                    patch.IsFavorite ?? slot.IsFavorite,
                    patch.DateAndTime ?? slot.DateAndTime,
                    patch.Title ?? slot.Title,
                    patch.Description ?? slot.Description,
                    patch.ImgPath ?? slot.ImgPath,
                    patch.TextureFolder ?? slot.TextureFolder,
                    patch.Sessions ?? slot.Sessions,
                    slot.Identifier // always keep original
                );
            }

            //C
            public static async Task AddDataSlot(DataSlot DataSlot)
            {
                var directoryPath = Path.Combine(DataPath, "DataSlot", DataSlot.Identifier);

                await InitiateDataSlot(DataSlot.Identifier);


                var jsonFile = await DataHandler.JSONDataHandler.LoadJsonFile(DataSlot.Identifier, directoryPath);

                jsonFile = await DataHandler.JSONDataHandler.UpdateJson<DataSlot>(jsonFile, "Data", DataSlot, encryptionKey);

                await DataHandler.JSONDataHandler.SaveJson(jsonFile);

            }
            //R
            public static List<string> GetDataSlot()
            {
                var basePath = Path.Combine(DataPath, "DataSlot");

                // Get all directories inside basePath, then just take the folder names
                return Directory.GetDirectories(basePath)
                                .Select(Path.GetFileName)
                                .ToList();
            }


            public static async Task<DataSlot> GetDataSlot(string identifier)
            {
                var directoryPath = Path.Combine(DataPath, "DataSlot", identifier);

                var filePath = Path.Combine(directoryPath, identifier + ".json");

                if (!File.Exists(filePath))
                {
                    throw new InvalidOperationException("This DataSlot does not exist.");

                }

                var jsonFile = await DataHandler.JSONDataHandler.LoadJsonFile(identifier, directoryPath);

                var point = (DataSlot)await DataHandler.JSONDataHandler.GetVariable<DataSlot>(jsonFile, "Data", encryptionKey);

                return point;

            }

            //U
            public static async Task UpdateDataSlot(DataSlot DataSlot)
            {
                var directoryPath = Path.Combine(DataPath, "DataSlot", DataSlot.Identifier);

                var jsonFile = await DataHandler.JSONDataHandler.LoadJsonFile(DataSlot.Identifier, directoryPath);

                jsonFile = await DataHandler.JSONDataHandler.UpdateJson<DataSlot>(jsonFile, "Data", DataSlot, encryptionKey);

                await DataHandler.JSONDataHandler.SaveJson(jsonFile);

            }
            //D
            public static void DeleteDataSlot(string identifier)
            {
                var directoryPath = Path.Combine(DataPath, "DataSlot", identifier);

                if (!Directory.Exists(directoryPath))
                    throw new InvalidOperationException("This DataSlot does not exist.");

                Directory.Delete(directoryPath, true); // delete folder + contents
            }


            public static async Task InitiateDataSlot(string identifier)
            {
                var basePath = Path.Combine(DataPath, "DataSlot");
                Directory.CreateDirectory(basePath);

                var slotPath = Path.Combine(basePath, identifier);
                Directory.CreateDirectory(slotPath);

                var filePath = Path.Combine(slotPath, identifier + ".json");

                if (!File.Exists(filePath))
                {
                    await JSONDataHandler.CreateJsonFile(identifier, slotPath, new JsonObject());

                    var jsonFile = await JSONDataHandler.LoadJsonFile(identifier, slotPath);
                    jsonFile = await JSONDataHandler.AddToJson<DataManagerClass.DataSlotClass.DataSlot>(
                        jsonFile,
                        "Data",
                        new DataManagerClass.DataSlotClass.DataSlot(),
                        encryptionKey
                    );

                    await JSONDataHandler.SaveJson(jsonFile);
                }
            }



        }


        public class DataSlotFavoritesClass
        {

            //C
            public static async Task AddToFavorites(string dataSlotIdentifier, string directoryUUID)
            {
                var directoryPath = Path.Combine(DataPath, "DataSlot");
                var manager = new Bindings.DirectoryManager(directoryPath);
                await manager.LoadBindings();

                var favoritesFile = await DataHandler.JSONDataHandler.LoadJsonFile("DataSlotFavorites", directoryPath);
                var favorites = (List<FileRecord>)await DataHandler.JSONDataHandler.GetVariable<List<FileRecord>>(favoritesFile, "Data", encryptionKey);

                if (!favorites.Any(d => d.UUID == directoryUUID && d.File == dataSlotIdentifier))
                {
                    favorites.Add(new FileRecord(directoryUUID, dataSlotIdentifier));
                }
                else
                {
                    throw new InvalidOperationException($"DataSlot already favorited.");
                }

                var editedJSON = await DataHandler.JSONDataHandler.UpdateJson<List<FileRecord>>(favoritesFile, "Data", favorites, encryptionKey);
                await DataHandler.JSONDataHandler.SaveJson(editedJSON);
            }



            //R
            public static async Task<(List<string>, List<string>)> GetFavorites()
            {
                var directoryPath = Path.Combine(DataPath, "DataSlot");
                var manager = new Bindings.DirectoryManager(directoryPath);
                await manager.LoadBindings();

                var favoritesFile = await DataHandler.JSONDataHandler.LoadJsonFile("DataSlotFavorites", directoryPath);
                var favorites = (List<FileRecord>)await DataHandler.JSONDataHandler.GetVariable<List<FileRecord>>(favoritesFile, "Data", encryptionKey);

                var resolvedPaths = new List<string>();
                var unresolvedNames = new List<string>();

                foreach (var record in favorites)
                {
                    var folderPath = await manager.GetDirectoryById(record.UUID);
                    if (folderPath == null)
                    {
                        unresolvedNames.Add(record.File);
                        continue;
                    }

                    var jsonPath = Path.Combine(folderPath, record.File + ".json");

                    if (File.Exists(jsonPath))
                    {
                        resolvedPaths.Add(jsonPath);
                    }
                    else
                    {
                        unresolvedNames.Add(record.File);
                    }
                }

                return (resolvedPaths, unresolvedNames);
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



            public static async Task RemoveFromFavorites(string dataSlotIdentifier, string directoryUUID)
            {
                var directoryPath = Path.Combine(DataPath, "DataSlot");
                var manager = new Bindings.DirectoryManager(directoryPath);
                await manager.LoadBindings();

                var favoritesFile = await DataHandler.JSONDataHandler.LoadJsonFile("DataSlotFavorites", directoryPath);
                var favorites = (List<FileRecord>)await DataHandler.JSONDataHandler.GetVariable<List<FileRecord>>(favoritesFile, "Data", encryptionKey);

                var removedCount = favorites.RemoveAll(d => d.UUID == directoryUUID && d.File == dataSlotIdentifier);
                if (removedCount == 0)
                    throw new InvalidOperationException($"DataSlot not favorited.");

                var editedJSON = await DataHandler.JSONDataHandler.UpdateJson<List<FileRecord>>(favoritesFile, "Data", favorites, encryptionKey);
                await DataHandler.JSONDataHandler.SaveJson(editedJSON);
            }


            public static class Current
            {
                public static ObservableProperty<SessionClass.Session> CurrentSession;
            }



            //Create "Get only resolved paths" and "Filter unresolved"
        }
    }
}
