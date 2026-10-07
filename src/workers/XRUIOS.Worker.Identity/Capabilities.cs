using System.Text.RegularExpressions;
using EclipseProject;
using XRUIOS.Barebones;
using XRUIOS.Barebones.Functions;
using XRUIOS.Interpreter;

namespace XRUIOS.Worker.Identity
{
    // XRUIOS user PROFILES: the settings and names that belong to a person on this device.
    //
    // Deliberately NOT exposed: creating an operating-system account and checking an OS password. Those
    // are the real security-critical operations (a granted app could create accounts, or guess passwords
    // against a real account), so they stay inside the class until a safe way to offer them exists.
    // Everything here touches XRUIOS data only, never the OS account list.
    public static class IdentityCapabilities
    {
        private static readonly Regex SafeName = new(@"^[\p{L}\p{N}][\p{L}\p{N} ._'-]{0,59}$", RegexOptions.Compiled);
        private static readonly Regex SafeOsUser = new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.Compiled);
        // Profile ids are digit strings made by the class; the id becomes a folder name.
        private static readonly Regex SafeId = new(@"^[0-9]{10,60}$", RegexOptions.Compiled);

        [SeaOfDirac("Identity.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Identity: " + input;

        /// <summary>"id|osUser|firstName|nickname|profileImage;..." for the users on this device.</summary>
        [SeaOfDirac("ListUsers", new string[0], typeof(string))]
        public static async Task<string> ListUsers()
        {
            var users = await UserManager.ListPublicUsersAsync(global::XRUIOS.Barebones.XRUIOS.encryptionKey);
            return string.Join(';', users.Select(u =>
                $"{Wire.Field(u.Uuid)}|{Wire.Field(u.OsUser)}|{Wire.Field(u.FirstName)}|{Wire.Field(u.Nickname)}|{Wire.Field(u.ProfileImagePath)}"));
        }

        /// <summary>The id of the main user, or empty when there is none.</summary>
        [SeaOfDirac("MainUser", new string[0], typeof(string))]
        public static string MainUser() => UserManager.MainAccountId() ?? "";

        /// <summary>
        /// Create a profile for an existing OS account name. No OS account is created and no password is
        /// involved. Returns the new profile id.
        /// </summary>
        [SeaOfDirac("CreateProfile", new[] { "osUser", "firstName", "nickname" }, typeof(string), typeof(string), typeof(string), typeof(string))]
        public static async Task<string> CreateProfile(string osUser, string firstName, string nickname = "")
        {
            if (string.IsNullOrEmpty(osUser) || !SafeOsUser.IsMatch(osUser)) throw new ArgumentException("That is not an account name.");
            if (string.IsNullOrEmpty(firstName) || !SafeName.IsMatch(firstName)) throw new ArgumentException("A first name of up to 60 letters, digits or spaces is needed.");
            if (!string.IsNullOrEmpty(nickname) && !SafeName.IsMatch(nickname)) throw new ArgumentException("A nickname of up to 60 letters, digits or spaces.");

            var account = new UserManager.AccountInfo
            {
                firstName = firstName,
                nickname = string.IsNullOrEmpty(nickname) ? firstName : nickname,
                nameOrNicknamePreferred = string.IsNullOrEmpty(nickname) ? UserManager.NamingPref.Name : UserManager.NamingPref.Nickname,
                WindowsUser = osUser,
                profileImagePath = "",
                timeZoneInfo = TimeZoneInfo.Local.Id,
                mainLanguage = System.Globalization.CultureInfo.CurrentUICulture.Name,
            };

            string id = await new UserManager().CreateUserAsync(account, global::XRUIOS.Barebones.XRUIOS.encryptionKey, manageOsAccount: false)
                ?? throw new InvalidOperationException("The profile could not be created.");
            return id;
        }

        /// <summary>Delete a profile (its settings and public copy). The OS account is not touched.</summary>
        [SeaOfDirac("EraseProfile", new[] { "id" }, typeof(string), typeof(string))]
        public static async Task<string> EraseProfile(string id)
        {
            if (string.IsNullOrEmpty(id) || !SafeId.IsMatch(id)) throw new ArgumentException("That is not a profile id.");
            if (!await new UserManager().EraseAccountAsync(id)) throw new KeyNotFoundException($"No profile with id '{id}'.");
            return "erased";
        }
    }
}
