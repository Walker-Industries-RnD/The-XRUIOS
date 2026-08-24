using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;

namespace XRUIOS.Manager
{
    // The login gate. Each user's master key is wrapped under an Argon2id key derived from their XRUIOS
    // password and stored in a public login file (world-readable so a login screen can list users;
    // owner-writable by NTFS ownership). While the OS session is active, a copy is also sealed with the
    // platform vault (DPAPI on Windows) so the password isn't re-typed on every start.
    //
    // Nothing the Manager guards decrypts until one of the Login methods returns the master key.
    public sealed class LoginService
    {
        private readonly string _usersDir; // <PublicDataPath>/Users

        public LoginService(string publicDataPath)
        {
            _usersDir = Path.Combine(publicDataPath, "Users");
            Directory.CreateDirectory(_usersDir);
        }

        // The public login file. Holds no secret in the clear: the master key is AES-GCM wrapped under
        // the Argon2 key, and only the salt + wrapped bytes + KDF params are stored.
        private sealed record LoginVector(
            string Uuid, string Username, string? ProfileImagePath,
            string Argon2Salt, string WrapNonce, string WrappedMasterKey, string WrapTag,
            int Iterations, int MemoryKiB, int Parallelism);

        private string LoginFile(string uuid) => Path.Combine(_usersDir, uuid + ".login.json");
        private string SealFile(string uuid) => Path.Combine(_usersDir, uuid + ".osseal");

        public bool UserExists(string uuid) => File.Exists(LoginFile(uuid));

        // For a login screen: every user's id, name, and avatar, read from the public files.
        public IEnumerable<(string Uuid, string Username, string? ProfileImagePath)> ListUsers()
        {
            foreach (var f in Directory.GetFiles(_usersDir, "*.login.json"))
            {
                LoginVector? v = null;
                try { v = JsonSerializer.Deserialize<LoginVector>(File.ReadAllText(f)); } catch { }
                if (v is not null) yield return (v.Uuid, v.Username, v.ProfileImagePath);
            }
        }

        // First-time setup: mint a master key, wrap it under the XRUIOS password, write the public file,
        // and seal a session copy. Returns the master key so the caller can unlock the key authority.
        public byte[] CreateUser(string uuid, string username, string xruiosPassword, string? profileImagePath)
        {
            byte[] masterKey = RandomNumberGenerator.GetBytes(32);
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            var p = Argon2Params.Default;

            byte[] kek = DeriveKek(xruiosPassword, salt, p);
            var (nonce, ct, tag) = Wrap(kek, masterKey);
            CryptographicOperations.ZeroMemory(kek);

            WritePublicFile(new LoginVector(uuid, username, profileImagePath,
                B64(salt), B64(nonce), B64(ct), B64(tag), p.Iterations, p.MemoryKiB, p.Parallelism));
            TrySeal(uuid, masterKey);
            return masterKey;
        }

        // Cold login: derive the Argon2 key from the typed password and unwrap. Null on a wrong password
        // (the GCM tag fails to verify, which is indistinguishable from a corrupt file, by design).
        public byte[]? LoginWithPassword(string uuid, string xruiosPassword)
        {
            var v = ReadPublicFile(uuid);
            if (v is null) return null;

            byte[] kek = DeriveKek(xruiosPassword, FromB64(v.Argon2Salt),
                new Argon2Params(v.Iterations, v.MemoryKiB, v.Parallelism));
            try
            {
                byte[] master = Unwrap(kek, FromB64(v.WrapNonce), FromB64(v.WrappedMasterKey), FromB64(v.WrapTag));
                TrySeal(uuid, master); // refresh the session seal on a successful password login
                return master;
            }
            catch (CryptographicException)
            {
                return null;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(kek);
            }
        }

        // Seamless login: if this OS session already holds a valid seal, return the master key with no
        // password prompt. Null when there's no seal (cold machine, different user) or the platform has
        // no vault wired up.
        public byte[]? TrySealedLogin(string uuid) => TryUnseal(uuid);

        public void ClearSeal(string uuid)
        {
            try { if (File.Exists(SealFile(uuid))) File.Delete(SealFile(uuid)); } catch { }
        }

        // --- Argon2id ---
        private readonly record struct Argon2Params(int Iterations, int MemoryKiB, int Parallelism)
        {
            public static Argon2Params Default => new(3, 65536, 4); // 64 MiB, 3 passes, 4 lanes
        }

        private static byte[] DeriveKek(string password, byte[] salt, Argon2Params p)
        {
            using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                Iterations = p.Iterations,
                MemorySize = p.MemoryKiB,
                DegreeOfParallelism = p.Parallelism,
            };
            return argon2.GetBytes(32);
        }

        // --- AES-256-GCM wrap/unwrap ---
        private static (byte[] nonce, byte[] ct, byte[] tag) Wrap(byte[] kek, byte[] plaintext)
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] ct = new byte[plaintext.Length];
            byte[] tag = new byte[16];
            using var gcm = new AesGcm(kek, 16);
            gcm.Encrypt(nonce, plaintext, ct, tag);
            return (nonce, ct, tag);
        }

        private static byte[] Unwrap(byte[] kek, byte[] nonce, byte[] ct, byte[] tag)
        {
            byte[] pt = new byte[ct.Length];
            using var gcm = new AesGcm(kek, 16);
            gcm.Decrypt(nonce, ct, tag, pt);
            return pt;
        }

        // --- OS seal: Windows DPAPI. Linux/macOS left as a no-op until libsecret/Keychain is wired,
        //     so those platforms fall back to the password each cold start. ---
        private void TrySeal(string uuid, byte[] masterKey)
        {
            if (!OperatingSystem.IsWindows()) return;
            try
            {
                byte[] blob = System.Security.Cryptography.ProtectedData.Protect(
                    masterKey, Entropy(uuid), System.Security.Cryptography.DataProtectionScope.CurrentUser);
                File.WriteAllBytes(SealFile(uuid), blob);
            }
            catch { /* seal is a convenience; failure just means a password prompt next time */ }
        }

        private byte[]? TryUnseal(string uuid)
        {
            if (!OperatingSystem.IsWindows() || !File.Exists(SealFile(uuid))) return null;
            try
            {
                byte[] blob = File.ReadAllBytes(SealFile(uuid));
                return System.Security.Cryptography.ProtectedData.Unprotect(
                    blob, Entropy(uuid), System.Security.Cryptography.DataProtectionScope.CurrentUser);
            }
            catch { return null; }
        }

        private static byte[] Entropy(string uuid) => Encoding.UTF8.GetBytes("xruios-osseal:" + uuid);

        // --- public file IO ---
        private void WritePublicFile(LoginVector v)
        {
            File.WriteAllText(LoginFile(v.Uuid),
                JsonSerializer.Serialize(v, new JsonSerializerOptions { WriteIndented = true }));
        }

        private LoginVector? ReadPublicFile(string uuid)
        {
            if (!File.Exists(LoginFile(uuid))) return null;
            try { return JsonSerializer.Deserialize<LoginVector>(File.ReadAllText(LoginFile(uuid))); }
            catch { return null; }
        }

        private static string B64(byte[] b) => Convert.ToBase64String(b);
        private static byte[] FromB64(string s) => Convert.FromBase64String(s);
    }
}
