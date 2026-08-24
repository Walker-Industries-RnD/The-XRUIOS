using Ical.Net.CalendarComponents;
using Pariah_Cybersecurity;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Management;
using System.Security.Principal;
using Walker.Crypto;
using XRUIOS.Barebones;
using XRUIOS.Barebones.Functions;
using YuukoProtocol;
using static Pariah_Cybersecurity.DataHandler;
using static Pariah_Cybersecurity.DataHandler.DataRequest;
using static Walker.Crypto.SimpleAESEncryption;
using static XRUIOS.Barebones.AlarmClass;
using static XRUIOS.Barebones.ChronoClass;
using static XRUIOS.Barebones.ExperimentalAudioClass;
using static XRUIOS.Barebones.Functions.NoteClass;
using static XRUIOS.Barebones.Functions.NotificationClass;
using static XRUIOS.Barebones.Functions.ThemeSystem;
using static XRUIOS.Barebones.Functions.VolumeClass;
using static XRUIOS.Barebones.GeoClass;
using static XRUIOS.Barebones.Interfaces.AlarmClass;
using static XRUIOS.Barebones.Interfaces.DeviceManagerClass;
using static XRUIOS.Barebones.Interfaces.ExperimentalAudioClass;
using static XRUIOS.Barebones.Interfaces.GeoClass;
using static XRUIOS.Barebones.Interfaces.NoteClass;
using static XRUIOS.Barebones.Interfaces.NotificationClass;
using static XRUIOS.Barebones.Interfaces.Songs.Playlists;
using static XRUIOS.Barebones.Interfaces.SoundEQClass;
using static XRUIOS.Barebones.Interfaces.VolumeClass;
using static XRUIOS.Barebones.Interfaces.WorldEventsClass;
using static XRUIOS.Barebones.SoundEQClass;
using static XRUIOS.Barebones.XRUIOS;
using Alarm = XRUIOS.Barebones.Interfaces.AlarmClass.Alarm;
using JsonObject = System.Text.Json.Nodes.JsonObject;


namespace XRUIOS_UserManager
//This is the only class allowed to run before the user is logged in, given this replaces the default login systems the system may hold (to a degree) (Okay technically identity classes too LOL)
//The API is made in a way so that somehow gaining access to the rest of the API still leaves it impossible to use without having logged in first


//Built off the old system, albeit with better implementations and cross compatibility across MacOs, Linux and Windows (With Android and IOS to possibly be supported one day)
{
    public class XRUIOS_UserManager
    {


        public static async Task InitializeSystemAsync()
        {
            // Ensure root data folder exists early so downstream Create/Save calls have a base path
            try
            {
                if (!Directory.Exists(DataPath))
                    Directory.CreateDirectory(DataPath);

                Console.WriteLine($"  [INFO] DataPath = {DataPath}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [ERROR] Unable to ensure DataPath: {ex.GetType().Name}: {ex.Message}");
                Console.ResetColor();
                throw;
            }



            var initTasks = new List<Func<Task>>
    {
        async () => { PrintWithPause("[NERV] → Base filesystem check... OK"); },
        async () => { await InitiateAlarm();               PrintWithPause("[ALARM] → Angel detection grid online"); },
        async () => { await InitiateAppFavorites();        PrintWithPause("[DOCK] → Favorite Evangelion loadouts synced"); },
        async () => { await InitiateChrono();              PrintWithPause("[CHRONO] → Temporal anchor established (Tokyo-3 timezone)"); },
        async () => { await InitiateDataManagerFavorites();PrintWithPause("[DATASLOT] → Spatial bookmark favorites loaded"); },
        async () => { await InitiateLocationHistory();     PrintWithPause("[GEO] → Absolute coordinate log initialized"); },
        async () => { await InitiateRelativeCoordinates(); PrintWithPause("[GEO] → Relative AR jitter buffer active"); },
        async () => { await InitiateMediaAlbumClassFavorites(); PrintWithPause("[MEDIA] → Album favorites secured"); },
        async () => { await InitiateMediaTaggerFavorites();PrintWithPause("[TAGGER] → Creator metadata vault opened"); },
        async () => { await InitiateNoteFavorites();       PrintWithPause("[JOURNAL] → Favorite entry anchors locked"); },
        async () => { await InitiateNoteHistory();         PrintWithPause("[JOURNAL] → Edit history black box recording"); },
        async () => { await InitiateNotifications();       PrintWithPause("[NOTIFY] → Angel alert queue primed"); },
        async () => { await InitiateRecentlyRecorded();    PrintWithPause("[RECORD] → Last-captured spatial fragments stored"); },
        async () => { await InitiateSoundEQDB();           PrintWithPause("[AUDIO] → Default EQ profile \"ENTRY PLUG\" loaded"); },
        async () => { await InitiateVolume();              PrintWithPause("[AUDIO] → Volume matrix \"NERV BRIDGE MIX\" applied"); },
        async () => { await InitializeExperimentalVolume();     PrintWithPause("[AUDIO] → Experimental EVA Audio Systems Initialized"); },
        async () => { await InitiateCalendar();            PrintWithPause("[SCHEDULE] → Event calendar database initialized"); },
        async () => { await InitiateWorldPoint();          PrintWithPause("[SPATIAL] → World point anchor system primed"); },
                async () => { await InitiateTheme();          PrintWithPause("[THEME] → System theme slots created."); },
                async () => {await InitiateWorldEvents();          PrintWithPause("[EVENTS] → World Event Container Created");},
                                async () => {await InitiatMusic();          PrintWithPause("[MUSIC] → Music Handling systems created."); },
                                                        async () => {await InitiateDevices();          PrintWithPause("[DEVICES] → Device Identity systems created."); }
            };

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n  [WARNING] LCL pressure stabilizing...");
            Console.ResetColor();

            int step = 1;
            foreach (var task in initTasks)
            {
                try
                {
                    await task();
                    await Task.Delay(10); // half-second dramatic pauses between each subsystem
                }
                catch (Exception ex)
                {
                    // Log the error and continue: one failing subsystem should not stop the whole init
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n  [ERROR] Initialization step #{step} failed: {ex.GetType().Name}: {ex.Message}");
                    Console.ResetColor();

                    // Print stack on debug runs to help find the source of failure
                    Console.WriteLine(ex.ToString()); 
                    // continue to next subsystem
                }
                step++;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n  [ALL SYSTEMS NOMINAL]");
            Console.WriteLine("  >> XRUIOS SPATIAL CORE ONLINE <<");
            Console.WriteLine("  Pilot authorization acknowledged.");
            Console.ResetColor();

            await Task.Delay(10);
        }

        private static void PrintWithPause(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write("  [NERV] ");
            Console.ResetColor();
            Console.WriteLine(message);
            Thread.Sleep(1); // tiny dramatic beat
        }

        public static async Task InitiatMusic()
        {
            var directoryPath = Path.Combine(DataPath, "Music");

            var TagsPath = Path.Combine(DataPath, "Music Metadata");

            Directory.CreateDirectory(directoryPath);

            Directory.CreateDirectory(TagsPath);


            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile("MusicDirectory", directoryPath, new JsonObject());

            var musicDirFile = await JSONDataHandler.LoadJsonFile("MusicDirectory", directoryPath);
            musicDirFile = await JSONDataHandler.AddToJson<List<DirectoryRecord>>(musicDirFile, "Data", new List<DirectoryRecord>(), encryptionKey);

            await JSONDataHandler.SaveJson(musicDirFile);



            await JSONDataHandler.CreateJsonFile("MusicFavorites", directoryPath, new JsonObject());

            var musicFavFile = await JSONDataHandler.LoadJsonFile("MusicFavorites", directoryPath);
            musicFavFile = await JSONDataHandler.AddToJson<List<FileRecord>>(musicFavFile, "Data", new List<FileRecord>(), encryptionKey);

            await JSONDataHandler.SaveJson(musicFavFile);




            await JSONDataHandler.CreateJsonFile("MusicHistory", directoryPath, new JsonObject());

            var musicHistoryFile = await JSONDataHandler.LoadJsonFile("MusicHistory", directoryPath);
            musicHistoryFile = await JSONDataHandler.AddToJson<List<FileRecord>>(musicHistoryFile, "Data", new List<FileRecord>(), encryptionKey);

            await JSONDataHandler.SaveJson(musicHistoryFile);





            await JSONDataHandler.CreateJsonFile("Playlists", directoryPath, new JsonObject());

            var playlistsFile = await JSONDataHandler.LoadJsonFile("Playlists", directoryPath);
            playlistsFile = await JSONDataHandler.AddToJson<List<Playlist>>(playlistsFile, "Data", new List<Playlist>(), encryptionKey);

            await JSONDataHandler.SaveJson(playlistsFile);




        }

        public static async Task InitiateAlarm()
        {
            var directoryPath = Path.Combine(DataPath, "Alarms");
            Directory.CreateDirectory(directoryPath);
            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile("Alarms", directoryPath, new JsonObject());

            var alarmsFile = await JSONDataHandler.LoadJsonFile("Alarms", directoryPath);
            alarmsFile = await JSONDataHandler.AddToJson<ObservableCollection<Alarm>>(alarmsFile, "Data", new ObservableCollection<Alarm>(), encryptionKey);

            await JSONDataHandler.SaveJson(alarmsFile);
        }

        public static async Task InitiateAppFavorites()
        {
            var directoryPath = Path.Combine(DataPath, "App");
            Directory.CreateDirectory(directoryPath);
            var manager = new Bindings.DirectoryManager(directoryPath);

            // Create the favorites JSON file
            await JSONDataHandler.CreateJsonFile("AppFavorites", directoryPath, new JsonObject());

            // Load the correct file (AppFavorites, not App)
            var favoritesFile = await JSONDataHandler.LoadJsonFile("AppFavorites", directoryPath);

            // Initialize the "Data" key
            favoritesFile = await JSONDataHandler.AddToJson<List<FileRecord>>(favoritesFile, "Data", new List<FileRecord>(), encryptionKey);

            await JSONDataHandler.SaveJson(favoritesFile);
        }


        public static async Task InitiateChrono()
        {
            var directoryPath = Path.Combine(DataPath, "Chrono");
            Directory.CreateDirectory(directoryPath);
            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile("Chrono", directoryPath, new JsonObject());

            var chronoFile = await JSONDataHandler.LoadJsonFile("Chrono", directoryPath);
            chronoFile = await JSONDataHandler.AddToJson<DateData>(chronoFile, "Data", new DateData(), encryptionKey);

            await JSONDataHandler.SaveJson(chronoFile);
        }

        public static async Task InitiateDataManagerFavorites()
        {
            var directoryPath = Path.Combine(DataPath, "DataSlot");
            Directory.CreateDirectory(directoryPath);
            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile("DataSlotFavorites", directoryPath, new JsonObject());

            var alarmsFile = await JSONDataHandler.LoadJsonFile("DataSlotFavorites", directoryPath);
            alarmsFile = await JSONDataHandler.AddToJson<List<FileRecord>>(alarmsFile, "Data", new List<FileRecord>(), encryptionKey);

            await JSONDataHandler.SaveJson(alarmsFile);
        }

        public static async Task InitializeExperimentalVolume()
        {
            var expAudioPath = Path.Combine(DataPath, "ExpAudio");
            Directory.CreateDirectory(expAudioPath);
            var audioManager = new Bindings.DirectoryManager(expAudioPath);

            await JSONDataHandler.CreateJsonFile("ExpAudio", expAudioPath, new JsonObject());

            var expAudioJson = await JSONDataHandler.LoadJsonFile("ExpAudio", expAudioPath);
            expAudioJson = await JSONDataHandler.AddToJson<ExperimentalAudio>(
                expAudioJson, "Data", AdvancedAudioSettings, encryptionKey
            );

            await JSONDataHandler.SaveJson(expAudioJson);

            var masterVolPath = Path.Combine(DataPath, "MasterVol");
            Directory.CreateDirectory(masterVolPath);
            var volManager = new Bindings.DirectoryManager(masterVolPath);

            await JSONDataHandler.CreateJsonFile("MasterVol", masterVolPath, new JsonObject());

            var masterVolJson = await JSONDataHandler.LoadJsonFile("MasterVol", masterVolPath);
            masterVolJson = await JSONDataHandler.AddToJson<int>(
                masterVolJson, "Data", MasterVolume, encryptionKey
            );

            await JSONDataHandler.SaveJson(masterVolJson);
        }


        public static async Task InitiateLocationHistory()
        {
            var directoryPath = Path.Combine(DataPath, "Coords");
            Directory.CreateDirectory(directoryPath);
            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile("LocationData", directoryPath, new JsonObject());

            var coordFile = await JSONDataHandler.LoadJsonFile("LocationData", directoryPath);
            coordFile = await JSONDataHandler.AddToJson<List<LocationPoint>>(coordFile, "Data", new List<LocationPoint>(), encryptionKey);

            await JSONDataHandler.SaveJson(coordFile);
        }

        public static async Task InitiateRelativeCoordinates()
        {
            var directoryPath = Path.Combine(DataPath, "Coords");
            Directory.CreateDirectory(directoryPath);
            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile("RelativeLocationData", directoryPath, new JsonObject());

            var relativeCoordFile = await JSONDataHandler.LoadJsonFile("RelativeLocationData", directoryPath);
            relativeCoordFile = await JSONDataHandler.AddToJson<List<RelativeLocationPoint>>(relativeCoordFile, "Data", new List<RelativeLocationPoint>(), encryptionKey);

            await JSONDataHandler.SaveJson(relativeCoordFile);
        }



        public static async Task InitiateMediaAlbumClassFavorites()
        {
            var directoryPath = Path.Combine(DataPath, "MediaAlbum");
            Directory.CreateDirectory(directoryPath);

            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile("MediaAlbumFavorites", directoryPath, new JsonObject());

            var relativeCoordFile = await JSONDataHandler.LoadJsonFile("MediaAlbumFavorites", directoryPath);
            relativeCoordFile = await JSONDataHandler.AddToJson<List<FileRecord>>(relativeCoordFile, "Data", new List<FileRecord>(), encryptionKey);

            await JSONDataHandler.SaveJson(relativeCoordFile);
        }

        public static async Task InitiateMediaTaggerFavorites()
        {
            var directoryPath = Path.Combine(DataPath, "Creators");
            Directory.CreateDirectory(directoryPath);
            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile("Creators", directoryPath, new JsonObject());

            var relativeCoordFile = await JSONDataHandler.LoadJsonFile("Creators", directoryPath);
            relativeCoordFile = await JSONDataHandler.AddToJson<List<FileRecord>>(relativeCoordFile, "Data", new List<FileRecord>(), encryptionKey);

            await JSONDataHandler.SaveJson(relativeCoordFile);
        }

        public static async Task InitiateNoteFavorites()
        {
            var directoryPath = Path.Combine(DataPath, "Journal");
            Directory.CreateDirectory(directoryPath);
            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile("JournalFavorites", directoryPath, new JsonObject());

            var notesFile = await JSONDataHandler.LoadJsonFile("JournalFavorites", directoryPath);
            notesFile = await JSONDataHandler.AddToJson<List<string>>(notesFile, "Data", new List<string>(), encryptionKey);

            await JSONDataHandler.SaveJson(notesFile);
        }

        public static async Task InitiateNoteHistory()
        {
            var directoryPath = Path.Combine(DataPath, "Journal");
            Directory.CreateDirectory(directoryPath);
            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile("History", directoryPath, new JsonObject());

            var notesFile = await JSONDataHandler.LoadJsonFile("History", directoryPath);
            notesFile = await JSONDataHandler.AddToJson<List<HistoryEntry>>(notesFile, "Data", new List<HistoryEntry>(), encryptionKey);

            await JSONDataHandler.SaveJson(notesFile);
        }

        public static async Task InitiateNotifications()
        {
            var directoryPath = Path.Combine(DataPath, "NotificationHistory");
            Directory.CreateDirectory(directoryPath);
            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile("NotificationHistory", directoryPath, new JsonObject());

            var notificationsFile = await JSONDataHandler.LoadJsonFile("NotificationHistory", directoryPath);
            notificationsFile = await JSONDataHandler.AddToJson<List<NotificationContent>>(notificationsFile, "Data", new List<NotificationContent>(), encryptionKey);

            await JSONDataHandler.SaveJson(notificationsFile);
        }

        public static async Task InitiateRecentlyRecorded()
        {
            var directoryPath = Path.Combine(DataPath, "RecentlyRecorded");
            Directory.CreateDirectory(directoryPath);
            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile("RecentlyRecorded", directoryPath, new JsonObject());

            var recentlyRecorded = await JSONDataHandler.LoadJsonFile("RecentlyRecorded", directoryPath);
            recentlyRecorded = await JSONDataHandler.AddToJson<List<FileRecord>>(recentlyRecorded, "Data", new List<FileRecord>(), encryptionKey);

            await JSONDataHandler.SaveJson(recentlyRecorded);
        }

        public static async Task InitiateSoundEQDB()
        {
            var directoryPath = Path.Combine(DataPath, "EQDB");
            Directory.CreateDirectory(directoryPath);
            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile("EQDBData", directoryPath, new JsonObject());

            var favoritesFile = await JSONDataHandler.LoadJsonFile("EQDBData", directoryPath);
            favoritesFile = await JSONDataHandler.AddToJson<List<SoundEQ>>(favoritesFile, "Data", new List<SoundEQ>(), encryptionKey);

            var fancyoptions = new ExperimentalAudio(false, false, 0, 0);
            var loaded = new SoundEQ("Default", 100, 100, 100, 100, 100, 100, 100, fancyoptions);

            await JSONDataHandler.AddToJson<SoundEQ>(favoritesFile, "DefaultEQDB", loaded, encryptionKey);
            await JSONDataHandler.SaveJson(favoritesFile);
        }

        public static async Task InitiateVolume()
        {
            var directoryPath = Path.Combine(DataPath, "VolumeMixings");
            Directory.CreateDirectory(directoryPath);
            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile("VolumeMixings", directoryPath, new JsonObject());

            var favoritesFile = await JSONDataHandler.LoadJsonFile("VolumeMixings", directoryPath);
            favoritesFile = await JSONDataHandler.AddToJson<List<VolumeSetting>>(favoritesFile, "VolumeMixings", new List<VolumeSetting>(), encryptionKey);

            await JSONDataHandler.SaveJson(favoritesFile);
        }


        public static async Task InitiateCalendar()
        {
            var directoryPath = Path.Combine(DataPath, "Calendar");
            Directory.CreateDirectory(directoryPath);
            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile("Calendar", directoryPath, new JsonObject());

            var calendarFile = await JSONDataHandler.LoadJsonFile("Calendar", directoryPath);
            calendarFile = await JSONDataHandler.AddToJson<List<CalendarEvent>>(calendarFile, "Data", new List<CalendarEvent>(), encryptionKey);

            await JSONDataHandler.SaveJson(calendarFile);
        }

        public static async Task InitiateWorldPoint()
        {
            var directoryPath = Path.Combine(DataPath, "WorldPoint");
            Directory.CreateDirectory(directoryPath);
            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile("WorldPoint", directoryPath, new JsonObject());

            var worldPointFile = await JSONDataHandler.LoadJsonFile("WorldPoint", directoryPath);
            worldPointFile = await JSONDataHandler.AddToJson<List<AreaManagerClass.WorldPoint>>(worldPointFile, "Data", new List<AreaManagerClass.WorldPoint>(), encryptionKey);

            await JSONDataHandler.SaveJson(worldPointFile);
        }


        public static async Task InitiateTheme()
        {
            var directoryPath = Path.Combine(DataPath, "Themes");
            Directory.CreateDirectory(directoryPath);

        }

        public static async Task InitiateWorldEvents()
        {
            var directoryPath = Path.Combine(DataPath, "WorldEvents");
            Directory.CreateDirectory(directoryPath);
            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile("WorldEvents", directoryPath, new JsonObject());

            var recentlyRecorded = await JSONDataHandler.LoadJsonFile("WorldEvents", directoryPath);
            recentlyRecorded = await JSONDataHandler.AddToJson<List<WorldEvent>>(recentlyRecorded, "WorldEvents", new List<WorldEvent>(), encryptionKey);

            await JSONDataHandler.SaveJson(recentlyRecorded);
        }

        public static async Task InitiateDevices()
        {
            var directoryPath = Path.Combine(DataPath, "Devices");
            Directory.CreateDirectory(directoryPath);

            var manager = new Bindings.DirectoryManager(directoryPath);

            await JSONDataHandler.CreateJsonFile(
                "Devices",
                directoryPath,
                new JsonObject());

            var deviceFile =
                await JSONDataHandler.LoadJsonFile(
                    "Devices",
                    directoryPath);

            deviceFile =
                await JSONDataHandler.AddToJson<List<Device>>(
                    deviceFile,
                    "Data",
                    new List<Device>(),
                    encryptionKey);

            await JSONDataHandler.SaveJson(deviceFile);
        }




    }


}
