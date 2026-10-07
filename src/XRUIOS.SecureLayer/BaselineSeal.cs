using System.Security.Cryptography;
using System.Text;
using static Pariah_Cybersecurity.EasyPQC;

namespace XRUIOS.Interfaces
{
    /// <summary>
    /// A worker's (or app's) approved file tree, signed by the Manager's identity key. This replaces
    /// trust-on-first-use: a folder is only trusted when the Manager signed its tree, and the Manager
    /// only signs when the user approves (<c>XRUIOS.Manager baseline</c> / <c>install-app</c>).
    ///
    /// The signature covers a canonical digest of the tree's CONTENT (sorted path + hash pairs), not the
    /// file bytes, so the verifier signs/checks exactly the dictionary it then compares against - there
    /// is no window between "checked the file" and "loaded the file".
    /// </summary>
    public static class BaselineSeal
    {
        /// <summary>Env var the Manager sets on a worker: path of its signed .tree.</summary>
        public const string TreeEnv = "XRUIOS_BASELINE";
        /// <summary>Env var the Manager sets on a worker: base64 signature over that tree.</summary>
        public const string SignatureEnv = "XRUIOS_BASELINE_SIG";

        /// <summary>
        /// Notary keys a tree by absolute path, but the same folder can be spelled differently by two
        /// processes (a long path vs its 8.3 short form, a moved install). Baselines are stored and
        /// compared with paths relative to the folder that was hashed, so only the content counts.
        /// </summary>
        public static Dictionary<string, byte[]> Relativize(IReadOnlyDictionary<string, byte[]> tree, string folder)
        {
            string root = folder.TrimEnd('\\', '/');
            var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var (path, hash) in tree)
            {
                string rel = path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                    ? path[root.Length..].TrimStart('\\', '/')
                    : path;
                result[rel.Replace('\\', '/')] = hash;
            }
            return result;
        }

        /// <summary>
        /// Blake3-hash every file under <paramref name="folder"/> and key the result by path relative to it.
        /// The hashes are the same ones Notary produces, so existing signed baselines stay valid. Files are
        /// read one at a time: Notary's own directory pass hashes up to 20 files at once, and each worker
        /// repeats the pass every 30 seconds, which grew every worker by ~100 MB of private memory.
        /// A file that cannot be opened is left out, as in Notary.
        /// </summary>
        public static async Task<Dictionary<string, byte[]>> HashFolderAsync(string folder)
        {
            var tree = new Dictionary<string, byte[]>();
            foreach (var file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
            {
                try
                {
                    using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read,
                        bufferSize: 81920, useAsync: true);
                    tree[file] = await FileOperations.HashFile(stream);
                }
                catch (IOException) { }
            }
            return Relativize(tree, folder);
        }

        public static string Digest(IReadOnlyDictionary<string, byte[]> tree)
        {
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var (path, hash) in tree.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                byte[] p = Encoding.UTF8.GetBytes(path);
                sha.AppendData(BitConverter.GetBytes(p.Length));
                sha.AppendData(p);
                sha.AppendData(BitConverter.GetBytes(hash.Length));
                sha.AppendData(hash);
            }
            return Convert.ToHexString(sha.GetHashAndReset());
        }

        public static Task<byte[]> SignAsync(Dictionary<string, byte[]> privateKey, IReadOnlyDictionary<string, byte[]> tree)
            => PqcGate.RunAsync(() => Signatures.CreateSignature(privateKey, Digest(tree)));

        public static async Task<bool> VerifyAsync(Dictionary<string, byte[]> publicKey, byte[] signature, IReadOnlyDictionary<string, byte[]> tree)
        {
            try { return await PqcGate.RunAsync(() => Signatures.VerifySignature(publicKey, signature, Digest(tree))); }
            catch { return false; }
        }
    }
}
