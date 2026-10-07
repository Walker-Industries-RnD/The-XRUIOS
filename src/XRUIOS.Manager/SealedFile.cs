using System.Security.Cryptography;

namespace XRUIOS.Manager
{
    // AES-256-GCM for the Manager's small private files (its identity key, the app registry).
    // Layout on disk: nonce(12) | tag(16) | ciphertext. A wrong key or any edited byte fails the tag.
    internal static class SealedFile
    {
        public static void Write(string path, byte[] key, byte[] plaintext)
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] tag = new byte[16];
            byte[] ct = new byte[plaintext.Length];
            using (var gcm = new AesGcm(key, 16))
                gcm.Encrypt(nonce, plaintext, ct, tag);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = path + ".tmp";
            File.WriteAllBytes(tmp, [.. nonce, .. tag, .. ct]);
            File.Move(tmp, path, overwrite: true);
        }

        /// <summary>The plaintext, or null when the file doesn't exist. Throws if it was tampered with.</summary>
        public static byte[]? Read(string path, byte[] key)
        {
            if (!File.Exists(path)) return null;
            byte[] all = File.ReadAllBytes(path);
            if (all.Length < 28) throw new CryptographicException($"{path} is truncated.");
            byte[] pt = new byte[all.Length - 28];
            using var gcm = new AesGcm(key, 16);
            gcm.Decrypt(all.AsSpan(0, 12), all.AsSpan(28), all.AsSpan(12, 16), pt);
            return pt;
        }
    }
}
