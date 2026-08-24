using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using WISecureData;
using static Pariah_Cybersecurity.DataHandler;   // JSONDataHandler
using static global::XRUIOS.Barebones.XRUIOS;     // DataPath / PublicDataPath

// The account lifecycle, rebuilt cross-platform (Windows + Linux) against the current API:
//   • OS credential checks go through the hardened auth (LogonUser / PAM) added in IdentityClass.Auth.cs.
//   • Profile persistence uses JSONDataHandler (no Bolt UniversalSave), encrypted with a SecureData
//     key ONLY the XRUIOS.Manager holds — never the shared dev encryptionKey. Pass that key in.
//   • Paths use DataPath / PublicDataPath (no C:\Users), so it runs the same on both OSes.
//
// No password is ever stored: login is a real OS authentication, so XRUIOS keeps only the user's
// settings profile, encrypted at rest under the Manager's key.

#nullable disable

namespace XRUIOS.Barebones.Functions
{
    public partial class UserManager
    {

        /// <summary>The user's settings profile — everything except credentials. Encrypted at rest.</summary>
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

            public UserProfile() { } // required for the serializer
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

        // C

        /// <summary>
        /// Create an XRUIOS user and its encrypted profile. Verifies (or creates) the OS account, then
        /// stores the profile under <paramref name="managerKey"/> — the key only the XRUIOS.Manager holds.
        /// Returns the new UUID, or null on failure.
        /// </summary>
        /// <param name="manageOsAccount">
        /// When true (default), the OS account is verified or created (LogonUser / New-LocalUser). Pass
        /// false to register the XRUIOS profile ONLY — no real OS user is created or authenticated. Use
        /// false for demos/tests, or when the OS account is provisioned elsewhere.
        /// </param>
        public async Task<string> CreateUserAsync(AccountInfo account, SecureData managerKey, bool manageOsAccount = true)
        {
            string accname = account.nameOrNicknamePreferred == NamingPref.Nickname ? account.nickname : account.firstName;

            // 1. Handle the OS account (cross-platform via the hardened auth). Skipped entirely when
            //    manageOsAccount is false — the profile-only path that touches nothing outside XRUIOS.
            if (manageOsAccount)
            {
                bool exists = GetRealLocalAccounts()
                    .Any(a => string.Equals(a, account.WindowsUser, StringComparison.OrdinalIgnoreCase));

                if (exists)
                {
                    // The account exists — prove the caller is entitled to it. On Linux, when it's THIS
                    // session's user we bind with no password and no root (getpwuid(geteuid()) via
                    // BindToCurrentUser) — the unprivileged companion-app path. Otherwise (or on Windows)
                    // require a real OS login: LogonUser on Windows, full PAM on Linux.
                    bool boundAsCurrentUser = !OperatingSystem.IsWindows() && BindToCurrentUser(account.WindowsUser);
                    if (!boundAsCurrentUser)
                    {
                        using var pw = ToSecureString(account.WindowsPass);
                        if (!AuthenticateOsLogin(account.WindowsUser, pw))
                        {
                            Console.Error.WriteLine("[Identity] OS credentials rejected — aborting.");
                            return null;
                        }
                    }
                }
                else if (OperatingSystem.IsWindows())
                {
                    // First account on the machine becomes admin, everyone after is standard.
                    bool firstUser = GetLocalUserCount() == 0;
                    if (firstUser)
                        CreateUserAndAddToAdministrators(account.WindowsUser, account.WindowsPass, accname, "Created by the XRUIOS");
                    else
                        CreateUserAndAddToStandard(account.WindowsUser, account.WindowsPass, accname, "Created by the XRUIOS");
                }
                else
                {
                    // Non-Windows and the account isn't in the local passwd list. We can't *create* an OS
                    // account from the companion build (that needs useradd + root — the privileged greeter
                    // build's job). But the hardened Linux path CAN bind to the CURRENT logged-in user with
                    // no privilege via BindToCurrentUser (getpwuid(geteuid()) in LinuxAuthProvider) — which
                    // also covers a current user filtered out of the uid>=1000 enumeration. If the requested
                    // user is this session's owner, accept it; otherwise it genuinely doesn't exist here, so
                    // we refuse rather than bind a profile to a ghost account.
                    if (!BindToCurrentUser(account.WindowsUser))
                    {
                        Console.Error.WriteLine("[Identity] Non-Windows: no such local account and it isn't the current user — aborting.");
                        return null;
                    }
                }
            }
            else
            {
                Console.WriteLine("[Identity] manageOsAccount=false — profile-only, no OS account created or authenticated.");
            }

            // 2. Mint a stable UUID and build the profile (no passwords are ever stored).
            string uuid = CreateUUID();
            var profile = ToProfile(account, uuid);

            // 3. Persist the profile, encrypted under the Manager's key.
            await SaveEncryptedAsync("Profile", Path.Combine(DataPath, "Users", uuid), profile, managerKey);

            // 4. Public discovery copy (also encrypted — only the Manager reads it).
            var pub = new PublicUserInfo
            {
                Uuid = uuid,
                OsUser = account.WindowsUser,
                FirstName = account.firstName,
                Nickname = account.nickname,
                ProfileImagePath = account.profileImagePath,
                CanConnectAcc = true
            };
            await SaveEncryptedAsync(uuid, Path.Combine(PublicDataPath, "Users"), pub, managerKey);

            // 5. Remember the main account (a file, so it persists cross-platform).
            SetMainAccount(uuid);
            return uuid;
        }

        // R

        /// <summary>
        /// Authenticate against the OS (LogonUser / PAM), then load the matching XRUIOS profile,
        /// decrypted with the Manager's key. Returns null if authentication fails or no profile is bound.
        /// </summary>
        public async Task<UserProfile> LoginAsync(string osUser, string password, SecureData managerKey)
        {
            using var pw = ToSecureString(password);
            if (!AuthenticateOsLogin(osUser, pw))
                return null; // wrong credentials — indistinguishable from "no such user", by design

            string uuid = await FindUuidForOsUserAsync(osUser, managerKey);
            if (uuid == null)
                return null;

            var profile = await LoadEncryptedAsync<UserProfile>("Profile", Path.Combine(DataPath, "Users", uuid), managerKey);
            if (profile != null)
                SetMainAccount(uuid);
            return profile;
        }

        // U


        /// <summary>Overwrite an existing profile in place, re-encrypted under the Manager's key.</summary>
        public async Task<bool> UpdateAccountAsync(string uuid, UserProfile updated, SecureData managerKey)
        {
            string userDir = Path.Combine(DataPath, "Users", uuid);
            if (!File.Exists(Path.Combine(userDir, "Profile.json")))
                return false;

            updated.Uuid = uuid;
            await SaveEncryptedAsync("Profile", userDir, updated, managerKey);

            // keep the public discovery copy in sync
            var pub = new PublicUserInfo
            {
                Uuid = uuid,
                OsUser = updated.OsUser,
                FirstName = updated.FirstName,
                Nickname = updated.Nickname,
                ProfileImagePath = updated.ProfileImagePath,
                CanConnectAcc = true
            };
            await SaveEncryptedAsync(uuid, Path.Combine(PublicDataPath, "Users"), pub, managerKey);
            return true;
        }

        // D

        /// <summary>Delete an account's private profile folder and its public copy.</summary>
        public Task<bool> EraseAccountAsync(string uuid)
        {
            bool removed = false;

            string userDir = Path.Combine(DataPath, "Users", uuid);
            if (Directory.Exists(userDir)) { Directory.Delete(userDir, recursive: true); removed = true; }

            string pubFile = Path.Combine(PublicDataPath, "Users", uuid + ".json");
            if (File.Exists(pubFile)) { File.Delete(pubFile); removed = true; }

            if (GetMainAccount() == uuid)
                SetMainAccount(null);

            return Task.FromResult(removed);
        }

        //helpers

        private static UserProfile ToProfile(AccountInfo a, string uuid) => new UserProfile
        {
            Uuid = uuid,
            OsUser = a.WindowsUser,
            Birthdate = a.birthdate,
            Gender = a.gender,
            FirstName = a.firstName,
            MiddleName = a.middleName,
            LastName = a.lastName,
            Nickname = a.nickname,
            NameOrNicknamePreferred = a.nameOrNicknamePreferred,
            ProfileImagePath = a.profileImagePath,
            Brightness = a.brightness,
            MinimumSleepTime = a.minimumSleepTime,
            IrisProtection = a.irisProtection,
            AntiEpilepsy = a.antiEpilepsy,
            Resolution = a.resolution,
            MasterVolume = a.masterVolume,
            SoftwareVolume = a.softwareVolume,
            EffectsVolume = a.effectsVolume,
            VoiceVolume = a.voiceVolume,
            MusicVolume = a.musicVolume,
            AlertVolume = a.alertVolume,
            UIVolume = a.uiVolume,
            EnvironmentFilter = a.environmentFilter,
            EnvironmentReduction = a.environmentReduction,
            DbLimitFilter = a.dbLimitFilter,
            ReduceVolumePercentage = a.reduceVolumePercentage,
            TimeFormat = a.timeFormat,
            TimeZoneInfo = a.timeZoneInfo,
            ShortTime = a.shorttime,
            ShortDate = a.shortdate,
            LongTime = a.longtime,
            LongDate = a.longdate,
            MainLanguage = a.mainLanguage,
            Level = 0
        };

        // Create or update an encrypted JSON store: CreateJsonFile throws if the file exists, so guard it.
        private static async Task SaveEncryptedAsync<T>(string name, string dir, T value, SecureData key)
        {
            Directory.CreateDirectory(dir);
            if (!File.Exists(Path.Combine(dir, name + ".json")))
                await JSONDataHandler.CreateJsonFile(name, dir, new JsonObject());

            var file = await JSONDataHandler.LoadJsonFile(name, dir);
            file = await JSONDataHandler.UpdateJson<T>(file, "Data", value, key);
            await JSONDataHandler.SaveJson(file);
        }

        private static async Task<T> LoadEncryptedAsync<T>(string name, string dir, SecureData key)
        {
            if (!File.Exists(Path.Combine(dir, name + ".json")))
                return default;
            var file = await JSONDataHandler.LoadJsonFile(name, dir);
            return (T)await JSONDataHandler.GetVariable<T>(file, "Data", key);
        }

        // Find which UUID is bound to an OS user by scanning the public copies (few users per device).
        private static async Task<string> FindUuidForOsUserAsync(string osUser, SecureData key)
        {
            string pubDir = Path.Combine(PublicDataPath, "Users");
            if (!Directory.Exists(pubDir)) return null;

            foreach (var file in Directory.GetFiles(pubDir, "*.json"))
            {
                string uuid = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var info = await LoadEncryptedAsync<PublicUserInfo>(uuid, pubDir, key);
                    if (info != null && string.Equals(info.OsUser, osUser, StringComparison.OrdinalIgnoreCase))
                        return uuid;
                }
                catch { /* unreadable/foreign entry — skip */ }
            }
            return null;
        }

        private static string MainAccountFile => Path.Combine(DataPath, "MainAccount");

        private static void SetMainAccount(string uuid)
        {
            Directory.CreateDirectory(DataPath);
            if (uuid == null) { if (File.Exists(MainAccountFile)) File.Delete(MainAccountFile); }
            else File.WriteAllText(MainAccountFile, uuid);
        }

        private static string GetMainAccount() => File.Exists(MainAccountFile) ? File.ReadAllText(MainAccountFile).Trim() : null;

        private static SecureString ToSecureString(string value)
        {
            var secure = new SecureString();
            if (value != null)
                foreach (char c in value) secure.AppendChar(c);
            secure.MakeReadOnly();
            return secure;
        }
    }
}
