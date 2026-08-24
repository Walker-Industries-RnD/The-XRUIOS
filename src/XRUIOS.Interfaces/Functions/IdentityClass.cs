using Org.BouncyCastle.Tsp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XRUIOS.Barebones.Interfaces.Functions
{
    public static class IdentityClass
    {

        public enum Gender { Male, Female, Other } //He, she, their, their

        public enum AccountType { WindowsAdmin, StandardUser, GuestUser, XRUIOSChild } //New windows acc, guest Windows acc

        public enum NamingPref { Name, Nickname }

        public enum TimeFormat { TwelveHour, TwentyFourHour }

        public enum ShortTime { hhdmm, hhpmm, hhdmmds, hhpmmps } //d = :, p = .

        public enum ShortDate { mmzddzyy, ddzmmzyy, mmxddxyy, ddxmmxyy, mmcddcyy, ddcmmcyy } //z = ., x = -, c = /

        public enum LongTime { EightThirthy, ThirtyMinutesPastEight, EightThirtyandTwentySeconds, EightMinutesandTwentySecondsPastEight }

        public enum LongDate { xxdaymmddyyyy, mmddyyyy, mmdd, ddmmyyyy }

        public enum CanConnectAccount { Yes, Some, No }

        public struct Resolution
        {
            public int width;
            public int height;
            public int refreshRate;

            public Resolution(int width, int height, int refreshRate)
            {
                this.width = width;
                this.height = height;
                this.refreshRate = refreshRate;
            }
        }

        //An easy way to get the default files and ensure everything is up to snuff when needed, else we just give the base account info and the systme takes care of the rest
        public struct XRUIOSDirectoryStructure
        {
            public string XRUIOSBaseFolder;
            public string UserFolder;
            public string ExternalsFolder;
            public string GeneralProfileFolder;
            public string XRUIOSDataFolder;
            public string AppsFolder;
            public string TempFolder;
            public string SavedObjectsFolder;
            public string SystemFilesDirectory;
            public string ProgramFilesDirectory;
            public string ProgramDataDirectory;
            public string FilesDirectory;
            public string User3DObjectsDirectory;
            public string AudioDirectory;
            public string DocumentsDirectory;
            public string DownloadsDirectory;
            public string GenericFilesDirectory;
            public string MediaDirectory;
            public string MusicDirectory;
            public string MusicPlaylistsDirectory;
            public string MusicAlbumsDirectory;
            public string MusicArtistsDirectory;
            public string OthersDirectory;
            public string PicturesDirectory;
            public string VideosDirectory;
            public string EnvironmentsDirectory;
            public string XRUIOSSystemVariables;


            public XRUIOSDirectoryStructure() { }


            public XRUIOSDirectoryStructure(string firstName, string nickname)
            {
                XRUIOSBaseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "XRUIOS Directory");
                Directory.CreateDirectory(XRUIOSBaseFolder);

                string genUserFolder = Path.Combine(XRUIOSBaseFolder, "Users");
                UserFolder = Path.Combine(genUserFolder, $"{firstName}{nickname}'s XRUIOS");
                Directory.CreateDirectory(UserFolder);

                ExternalsFolder = CreateAndReturnDirectory(UserFolder, "External");
                GeneralProfileFolder = CreateAndReturnDirectory(UserFolder, "GeneralProfileFolder");
                XRUIOSDataFolder = CreateAndReturnDirectory(UserFolder, "XRUIOSDataFolder");
                AppsFolder = CreateAndReturnDirectory(XRUIOSDataFolder, "Apps");
                TempFolder = CreateAndReturnDirectory(XRUIOSDataFolder, "Temp");
                SavedObjectsFolder = CreateAndReturnDirectory(XRUIOSDataFolder, "SavedObjects");
                SystemFilesDirectory = CreateAndReturnDirectory(UserFolder, "SystemFilesDirectory");
                ProgramFilesDirectory = CreateAndReturnDirectory(UserFolder, "ProgramFiles");
                ProgramDataDirectory = CreateAndReturnDirectory(UserFolder, "ProgramData");
                FilesDirectory = CreateAndReturnDirectory(UserFolder, "Files");
                User3DObjectsDirectory = CreateAndReturnDirectory(SystemFilesDirectory, "3DObjects");
                AudioDirectory = CreateAndReturnDirectory(SystemFilesDirectory, "Audio");
                DocumentsDirectory = CreateAndReturnDirectory(SystemFilesDirectory, "Documents");
                DownloadsDirectory = CreateAndReturnDirectory(SystemFilesDirectory, "Downloads");
                GenericFilesDirectory = CreateAndReturnDirectory(SystemFilesDirectory, "Files");
                MediaDirectory = CreateAndReturnDirectory(SystemFilesDirectory, "Media");
                MusicDirectory = CreateAndReturnDirectory(SystemFilesDirectory, "Music");
                MusicPlaylistsDirectory = CreateAndReturnDirectory(MusicDirectory, "MusicPlaylists");
                MusicAlbumsDirectory = CreateAndReturnDirectory(MusicDirectory, "MusicAlbums");
                MusicArtistsDirectory = CreateAndReturnDirectory(MusicDirectory, "MusicArtists");
                OthersDirectory = CreateAndReturnDirectory(SystemFilesDirectory, "Others");
                PicturesDirectory = CreateAndReturnDirectory(SystemFilesDirectory, "Pictures");
                VideosDirectory = CreateAndReturnDirectory(SystemFilesDirectory, "Videos");
                EnvironmentsDirectory = CreateAndReturnDirectory(SystemFilesDirectory, "Environments");
                XRUIOSSystemVariables = CreateAndReturnDirectory(SystemFilesDirectory, "XRUIOS");



            }

            private static string CreateAndReturnDirectory(string parentPath, string directoryName)
            {
                string path = Path.Combine(parentPath, directoryName);
                Directory.CreateDirectory(path);
                return path;
            }
        }



        public sealed class UserProfile
        {
            public string Uuid;
            public string OsUser;                 // the OS account this XRUIOS user is bound to

            public DateTime Birthdate;
            public Gender Gender;
            public string FirstName;
            public string MiddleName;
            public string LastName;
            public string Nickname;
            public NamingPref NameOrNicknamePreferred;
            public string ProfileImagePath;

            public int Brightness;
            public int MinimumSleepTime;
            public bool IrisProtection;
            public bool AntiEpilepsy;
            public Resolution Resolution;

            public int MasterVolume, SoftwareVolume, EffectsVolume, VoiceVolume, MusicVolume, AlertVolume, UIVolume;

            public bool EnvironmentFilter, EnvironmentReduction;
            public int DbLimitFilter, ReduceVolumePercentage;

            public TimeFormat TimeFormat;
            public string TimeZoneInfo;
            public ShortTime ShortTime;
            public ShortDate ShortDate;
            public LongTime LongTime;
            public LongDate LongDate;

            public string MainLanguage;
            public int Level;

            public UserProfile() { } 
        }

        /// <summary>The small public copy used to discover/list accounts before login.</summary>
        public sealed class PublicUserInfo
        {
            public string Uuid;
            public string OsUser;
            public string FirstName;
            public string Nickname;
            public string ProfileImagePath;
            public bool CanConnectAcc;

            public PublicUserInfo() { }
        }



        public static void CreatePublicXRUIOSDirectories()
        {
            //Let's get the base folder for the XRUIOS
            var universalpublicfolderpath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var XRUIOSBaseFolder = string.Concat(universalpublicfolderpath, "//XRUIOS Directory");


            // Public Files directory
            string publicFilesDirectory = Path.Combine(XRUIOSBaseFolder, "PublicFiles");
            Directory.CreateDirectory(publicFilesDirectory);

            // Users directory exists no more

            // Program Files directory
            string programFilesDirectory = Path.Combine(XRUIOSBaseFolder, "ProgramFiles");
            Directory.CreateDirectory(programFilesDirectory);

            // Program Data directory
            string programDataDirectory = Path.Combine(XRUIOSBaseFolder, "ProgramData");
            Directory.CreateDirectory(programDataDirectory);

            // PublicFiles subdirectories
            string public3DObjectsDirectory = Path.Combine(publicFilesDirectory, "3DObjects");
            Directory.CreateDirectory(public3DObjectsDirectory);

            string publicAudioDirectory = Path.Combine(publicFilesDirectory, "Audio");
            Directory.CreateDirectory(publicAudioDirectory);

            string publicDocumentsDirectory = Path.Combine(publicFilesDirectory, "Documents");
            Directory.CreateDirectory(publicDocumentsDirectory);

            string publicDownloadsDirectory = Path.Combine(publicFilesDirectory, "Downloads");
            Directory.CreateDirectory(publicDownloadsDirectory);

            string publicGenericFilesDirectory = Path.Combine(publicFilesDirectory, "Files");
            Directory.CreateDirectory(publicFilesDirectory);

            string publicMediaDirectory = Path.Combine(publicFilesDirectory, "Media");
            Directory.CreateDirectory(publicMediaDirectory);

            string publicMusicDirectory = Path.Combine(publicFilesDirectory, "Music");
            Directory.CreateDirectory(publicMusicDirectory);

            // Music subdirectories
            string mediaPlaylistsDirectory = Path.Combine(publicMusicDirectory, "MediaPlaylists");
            Directory.CreateDirectory(mediaPlaylistsDirectory);

            string musicPlaylistsDirectory = Path.Combine(publicMusicDirectory, "MusicPlaylists");
            Directory.CreateDirectory(musicPlaylistsDirectory);

            string musicArtistsDirectory = Path.Combine(publicMusicDirectory, "MusicArtists");
            Directory.CreateDirectory(musicArtistsDirectory);

            string musicAlbumsDirectory = Path.Combine(publicMusicDirectory, "MusicAlbums");
            Directory.CreateDirectory(musicAlbumsDirectory);

            // Others, Pictures, Videos, and Environments
            string othersDirectory = Path.Combine(publicFilesDirectory, "Others");
            Directory.CreateDirectory(othersDirectory);

            string picturesDirectory = Path.Combine(publicFilesDirectory, "Pictures");
            Directory.CreateDirectory(picturesDirectory);

            string videosDirectory = Path.Combine(publicFilesDirectory, "Videos");
            Directory.CreateDirectory(videosDirectory);

            string environmentsDirectory = Path.Combine(publicFilesDirectory, "Environments");
            Directory.CreateDirectory(environmentsDirectory);

            string userBasesDirectory = Path.Combine(XRUIOSBaseFolder, "User Bases");
            Directory.CreateDirectory(programFilesDirectory);




            //Finally, let's make these all system wide EnvironmentVariables!

            // Set environment variables with "XRUIOS_" prefix for each subdirectory

            Environment.SetEnvironmentVariable("XRUIOSBaseFolder", XRUIOSBaseFolder, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("XRUIOS_PublicFiles", publicFilesDirectory, EnvironmentVariableTarget.Machine);


            Environment.SetEnvironmentVariable("XRUIOS_PublicProgramFiles", programFilesDirectory, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("XRUIOS_PublicProgramData", programDataDirectory, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("XRUIOS_PublicFiles_3DObjects", public3DObjectsDirectory, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("XRUIOS_PublicFiles_Audio", publicAudioDirectory, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("XRUIOS_PublicFiles_Documents", publicDocumentsDirectory, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("XRUIOS_PublicFiles_Downloads", publicDownloadsDirectory, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("XRUIOS_PublicFiles_Files", publicGenericFilesDirectory, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("XRUIOS_PublicFiles_Media", publicMediaDirectory, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("XRUIOS_PublicFiles_Music", publicMusicDirectory, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("XRUIOS_PublicMusic_MediaPlaylists", mediaPlaylistsDirectory, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("XRUIOS_PublicMusic_MusicPlaylists", musicPlaylistsDirectory, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("XRUIOS_PublicMusic_MusicAlbums", musicAlbumsDirectory, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("XRUIOS_PublicMusic_MusicArtists", musicArtistsDirectory, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("XRUIOS_PublicFiles_Others", othersDirectory, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("XRUIOS_PublicFiles_Pictures", picturesDirectory, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("XRUIOS_PublicFiles_Videos", videosDirectory, EnvironmentVariableTarget.Machine);
            Environment.SetEnvironmentVariable("XRUIOS_PublicFiles_Environments", environmentsDirectory, EnvironmentVariableTarget.Machine);

            Environment.SetEnvironmentVariable("XRUIOS_UserBases", userBasesDirectory, EnvironmentVariableTarget.Machine);



        }
        public static void CreateUserLevelPaths(string UserFolder)
        {


            //Now we create all of the default paths and save them
            string externalsFolder = Path.Combine(UserFolder, "External");
            Directory.CreateDirectory(externalsFolder);

            string generalProfileFolder = Path.Combine(UserFolder, "GeneralProfileFolder");
            Directory.CreateDirectory(generalProfileFolder);

            string xruiosDataFolder = Path.Combine(UserFolder, "xruiosDataFolder");
            Directory.CreateDirectory(xruiosDataFolder);

            string appsFolder = Path.Combine(xruiosDataFolder, "Apps");
            Directory.CreateDirectory(appsFolder);

            string tempFolder = Path.Combine(xruiosDataFolder, "Temp");
            Directory.CreateDirectory(tempFolder);

            string pariahFolder = Path.Combine(xruiosDataFolder, "Pariah");
            Directory.CreateDirectory(pariahFolder);

            string savedObjectsFolder = Path.Combine(xruiosDataFolder, "SavedObjects");
            Directory.CreateDirectory(savedObjectsFolder);

            string systemFilesDirectory = Path.Combine(UserFolder, "SystemFilesDirectory");
            Directory.CreateDirectory(systemFilesDirectory);

            string programFilesDirectory = Path.Combine(UserFolder, "ProgramFiles");
            Directory.CreateDirectory(programFilesDirectory);

            string programDataDirectory = Path.Combine(UserFolder, "ProgramData");
            Directory.CreateDirectory(programDataDirectory);

            string FilesDirectory = Path.Combine(UserFolder, "Files");
            Directory.CreateDirectory(FilesDirectory);

            string user3DObjectsDirectory = Path.Combine(systemFilesDirectory, "3DObjects");
            Directory.CreateDirectory(user3DObjectsDirectory);

            string AudioDirectory = Path.Combine(systemFilesDirectory, "Audio");
            Directory.CreateDirectory(AudioDirectory);

            string DocumentsDirectory = Path.Combine(systemFilesDirectory, "Documents");
            Directory.CreateDirectory(DocumentsDirectory);

            string DownloadsDirectory = Path.Combine(systemFilesDirectory, "Downloads");
            Directory.CreateDirectory(DownloadsDirectory);

            string GenericFilesDirectory = Path.Combine(systemFilesDirectory, "Files");
            Directory.CreateDirectory(GenericFilesDirectory);

            string MediaDirectory = Path.Combine(systemFilesDirectory, "Media");
            Directory.CreateDirectory(MediaDirectory);

            string MusicDirectory = Path.Combine(systemFilesDirectory, "Music");
            Directory.CreateDirectory(MusicDirectory);

            string othersDirectory = Path.Combine(systemFilesDirectory, "Others");
            Directory.CreateDirectory(othersDirectory);

            string picturesDirectory = Path.Combine(systemFilesDirectory, "Pictures");
            Directory.CreateDirectory(picturesDirectory);

            string videosDirectory = Path.Combine(systemFilesDirectory, "Videos");
            Directory.CreateDirectory(videosDirectory);

            string environmentsDirectory = Path.Combine(systemFilesDirectory, "Environments");
            Directory.CreateDirectory(environmentsDirectory);
        }


    }
}
