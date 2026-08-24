using Walker.Crypto; 
using System.Diagnostics;
using System.Security.Principal;
using System.Management;

namespace XRUIOS.Barebones.Functions
{


    public partial class UserManager
    {

        public SimpleAESEncryption.AESEncryptedText LoggedInFirstName;

        public SimpleAESEncryption.AESEncryptedText LoggedInNickname;



        public enum Gender { Male, Female, Other } //He, she, their, their

        public enum AccountType { WindowsAdmin, StandardUser, GuestUser, XRUIOSChild } //New windows acc, guest Windows acc

        public enum NamingPref { Name, Nickname }

        public enum TimeFormat { TwelveHour, TwentyFourHour }

        public enum ShortTime { hhdmm, hhpmm, hhdmmds, hhpmmps } //d = :, p = .

        public enum ShortDate { mmzddzyy, ddzmmzyy, mmxddxyy, ddxmmxyy, mmcddcyy, ddcmmcyy } //z = ., x = -, c = /

        public enum LongTime { EightThirthy, ThirtyMinutesPastEight, EightThirtyandTwentySeconds, EightMinutesandTwentySecondsPastEight }

        public enum LongDate { xxdaymmddyyyy, mmddyyyy, mmdd, ddmmyyyy }

        public enum CanConnectAccount { Yes, Some, No }

        // Display resolution the user picked. Replaces the old UnityEngine.Resolution now that the
        // engine dependency is gone; same shape (width/height/refresh) so stored profiles are unaffected.
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

        public struct AccountInfo
        {
            public DateTime birthdate;
            public UserManager.Gender gender;

            public string firstName;
            public string middleName;
            public string lastName;
            public string nickname;
            public UserManager.NamingPref nameOrNicknamePreferred;

            public string profileImagePath;

            public int brightness;
            public int minimumSleepTime;
            public bool irisProtection;
            public bool antiEpilepsy;
            public Resolution resolution;

            public int masterVolume;
            public int softwareVolume;
            public int effectsVolume;
            public int voiceVolume;
            public int musicVolume;
            public int alertVolume;
            public int uiVolume;

            public bool environmentFilter;
            public bool environmentReduction;
            public int dbLimitFilter;
            public int reduceVolumePercentage;

            public UserManager.TimeFormat timeFormat;
            public string timeZoneInfo;
            public UserManager.ShortTime shorttime;
            public UserManager.ShortDate shortdate;
            public UserManager.LongTime longtime;
            public UserManager.LongDate longdate;

            public string mainLanguage;

            public string WindowsUser;
            public string WindowsPass;

            public string XRUIOSPass;
            public string PariahPass;

            public SimpleAESEncryption.AESEncryptedText XRUIOSUUID;
            public SimpleAESEncryption.AESEncryptedText PariahPassUUID;

            // Constructor method
            public AccountInfo(DateTime birthdate, UserManager.Gender gender, string firstName, string middleName,
                               string lastName, string nickname, UserManager.NamingPref nameOrNicknamePreferred,
                               string profileImagePath, int brightness, int minimumSleepTime, bool irisProtection,
                               bool antiEpilepsy, Resolution resolution, int masterVolume, int softwareVolume,
                               int effectsVolume, int voiceVolume, int musicVolume, int alertVolume, int uiVolume,
                               bool environmentFilter, bool environmentReduction, int dbLimitFilter,
                               int reduceVolumePercentage, UserManager.TimeFormat timeFormat, string timeZoneInfo,
                               UserManager.ShortTime shorttime, UserManager.ShortDate shortdate, UserManager.LongTime longtime,
                               UserManager.LongDate longdate, string mainLanguage, string WindowsUser, string WindowsPass,
                               string xruiospass, string pariahpass, SimpleAESEncryption.AESEncryptedText xruiosuuid, SimpleAESEncryption.AESEncryptedText pariahuuid)
            {
                this.birthdate = birthdate;
                this.gender = gender;
                this.firstName = firstName;
                this.middleName = middleName;
                this.lastName = lastName;
                this.nickname = nickname;
                this.nameOrNicknamePreferred = nameOrNicknamePreferred;
                this.profileImagePath = profileImagePath;
                this.brightness = brightness;
                this.minimumSleepTime = minimumSleepTime;
                this.irisProtection = irisProtection;
                this.antiEpilepsy = antiEpilepsy;
                this.resolution = resolution;
                this.masterVolume = masterVolume;
                this.softwareVolume = softwareVolume;
                this.effectsVolume = effectsVolume;
                this.voiceVolume = voiceVolume;
                this.musicVolume = musicVolume;
                this.alertVolume = alertVolume;
                this.uiVolume = uiVolume;
                this.environmentFilter = environmentFilter;
                this.environmentReduction = environmentReduction;
                this.dbLimitFilter = dbLimitFilter;
                this.reduceVolumePercentage = reduceVolumePercentage;
                this.timeFormat = timeFormat;
                this.timeZoneInfo = timeZoneInfo;
                this.shorttime = shorttime;
                this.shortdate = shortdate;
                this.longtime = longtime;
                this.longdate = longdate;
                this.mainLanguage = mainLanguage;
                this.WindowsUser = WindowsUser;
                this.WindowsPass = WindowsPass; //IMPORTANT NOTE: Windows Pass should NOT be null, the WindowsPass and XRUIOS pass are the same so we set this as the first pass val
                this.XRUIOSPass = xruiospass;
                this.XRUIOSUUID = xruiosuuid;
                this.PariahPass = pariahpass;
                this.PariahPassUUID = pariahuuid;
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


        public string CreateUUID()
        {
            string randomlocaluserid = default;

            for (int i = 0; i < 20; i++)
            {
                var tempitem = System.Random.Shared.Next(0, 20);
                var tempstring = tempitem.ToString();
                randomlocaluserid = string.Concat(randomlocaluserid, tempstring);
            }
            return randomlocaluserid;
        }



        void CreatePublicXRUIOSDirectories()
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



            Console.WriteLine("Your public folder is located at" + XRUIOSBaseFolder);
        }

        public List<string> GetUserAccountsWindows()
        {
            List<string> AccountsOnSystem = new List<string>();

            string userDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".");

            try
            {
                DirectoryInfo userDirectory = new DirectoryInfo(userDirectoryPath);
                foreach (DirectoryInfo user in userDirectory.EnumerateDirectories())
                {
                    AccountsOnSystem.Add(user.Name);
                }
            }
            catch (Exception ex)
            {
                // Handle exception as needed
                Console.Error.WriteLine("Error: " + ex.Message);
            }

            return AccountsOnSystem;
        }

        public bool ConnectAccToUser(string username, string password)
        {
            bool isConnectAllowed = default;

            string basePath = GetUserProfileDirectory(username);
            string xruiosPath = Path.Combine(basePath, "XRUIOS");
            string usersPath = Path.Combine(xruiosPath, "Users");

            if (Directory.Exists(xruiosPath))
            {
                string[] directories = Directory.GetDirectories(usersPath);

                if (directories.Length == 0)
                {
                    string query = $"SELECT * FROM Win32_UserAccount WHERE Name='{username}'";

                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                    {
                        ManagementObjectCollection results = searcher.Get();

                        foreach (ManagementObject user in results)
                        {
                            // Check if the user is passwordless
                            bool isPasswordless = Convert.ToBoolean(user["PasswordExpires"]);

                            if (isPasswordless)
                            {
                                isConnectAllowed = true;
                            }
                            else
                            {
                                // Check if the provided password is valid
                                ConnectionOptions connectionOptions = new ConnectionOptions
                                {
                                    Username = username,
                                    Password = password,
                                    Impersonation = ImpersonationLevel.Impersonate
                                };

                                ManagementScope scope = new ManagementScope($@"\\{Environment.MachineName}\root\cimv2", connectionOptions);

                                try
                                {
                                    scope.Connect();
                                    isConnectAllowed = true;
                                }
                                catch (UnauthorizedAccessException)
                                {
                                    // Invalid password
                                    isConnectAllowed = false;
                                }
                            }

                            break; // Assuming there is only one matching user
                        }
                    }
                }
            }
            else
            {
                // It is allowed since this is an empty ground!
                isConnectAllowed = true;
            }

            return isConnectAllowed;
        }

        static string GetUserProfileDirectory(string username)
        {
            string profilesDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            foreach (string directory in Directory.GetDirectories(profilesDirectory))
            {
                string sid = new DirectoryInfo(directory).Name;

                try
                {
                    SecurityIdentifier securityIdentifier = new SecurityIdentifier(sid);

                    NTAccount ntAccount = (NTAccount)securityIdentifier.Translate(typeof(NTAccount));

                    if (ntAccount.Value.Equals(username, StringComparison.OrdinalIgnoreCase))
                    {
                        return directory;
                    }
                }
                catch
                {
                    // Handle exceptions if necessary
                }
            }

            return null; // User profile directory not found
        }

        public static void CreateOmniPotentUser(string userName, string password, string fullName, string description)
        {
            CreateUserAndAddToAdministrators(userName, password, fullName, description);
            CreateUserAndAddToStandard(userName, password, fullName, description);
            CreateUserAndAddToGuests(userName, password, fullName, description);

            Console.WriteLine("User created and added to groups successfully.");
        }

        public static void CreateUserAndAddToAdministrators(string userName, string password, string fullName, string description)
        {
            string createUserCommand = $@"New-LocalUser -Name {userName} -Password (ConvertTo-SecureString '{password}' -AsPlainText -Force) -FullName '{fullName}' -Description '{description}'";
            string addGroupMemberCommand = $@"Add-LocalGroupMember -Group 'Administrators' -Member '{userName}'";

            string combinedCommands = $"{createUserCommand}; {addGroupMemberCommand}";

            RunPowerShellCommand(combinedCommands);
        } //To be used in the future

        public static void CreateUserAndAddToStandard(string userName, string password, string fullName, string description)
        {
            string createUserCommand = $@"New-LocalUser -Name {userName} -Password (ConvertTo-SecureString '{password}' -AsPlainText -Force) -FullName '{fullName}' -Description '{description}'";
            string addGroupMemberCommand = $@"Add-LocalGroupMember -Group 'Users' -Member '{userName}'";

            string combinedCommands = $"{createUserCommand}; {addGroupMemberCommand}";

            RunPowerShellCommand(combinedCommands);
        }

        public static void CreateUserAndAddToGuests(string userName, string password, string fullName, string description)
        {
            string createUserCommand = $@"New-LocalUser -Name {userName} -Password (ConvertTo-SecureString '{password}' -AsPlainText -Force) -FullName '{fullName}' -Description '{description}'";
            string addGroupMemberCommand = $@"Add-LocalGroupMember -Group 'Guests' -Member '{userName}'";

            string combinedCommands = $"{createUserCommand}; {addGroupMemberCommand}";

            RunPowerShellCommand(combinedCommands);
        } //To be used in the future

        public static void RunPowerShellCommand(string command)
        {
            using (Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            })
            {
                process.Start();

                process.StandardInput.WriteLine(command);
                process.StandardInput.WriteLine("exit");

                string output = process.StandardOutput.ReadToEnd();
                string errors = process.StandardError.ReadToEnd();

                process.WaitForExit();

                Console.WriteLine("PowerShell Output:");
                Console.WriteLine(output);

                if (!string.IsNullOrEmpty(errors))
                {
                    Console.WriteLine("PowerShell Errors:");
                    Console.WriteLine(errors);
                }
            }
        }



        //Check if login works

        public bool ValidateCredentials(string username, string password)
        {
            // PowerShell command to validate the user's credentials
            string script = $@"
            $username = '{username}'
            $password = '{password}'
            $user = Get-WmiObject -Class Win32_UserAccount -Filter ""Name='$username'""
            if ($user -ne $null) {{
                $securePassword = ConvertTo-SecureString $password -AsPlainText -Force
                $credential = New-Object System.Management.Automation.PSCredential($username, $securePassword)
                try {{
                    $credential.GetNetworkCredential() | Out-Null
                    $true
                }} catch {{
                    $false
                }}
            }} else {{
                $false
            }}
        ";

            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{script}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                string result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (bool.TryParse(result.Trim(), out bool isValid))
                {
                    return isValid;
                }
                else
                {
                    Console.Error.WriteLine("Failed to parse PowerShell output.");
                    return false;
                }
            }
        }


        public bool CheckIfUserIsLoggedIn()
        {
            string script = @"
        $user = query user
        if ($user) { $true } else { $false }
    ";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{script}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                string result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (bool.TryParse(result.Trim(), out bool isLoggedIn))
                    return isLoggedIn;

                Console.Error.WriteLine("Failed to parse PowerShell output.");
                return false;
            }
        }
    }
}
