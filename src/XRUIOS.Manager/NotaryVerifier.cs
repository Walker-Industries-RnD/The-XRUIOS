using NotaryCore;
using NotaryShared;
using XRUIOS.Interfaces;

namespace XRUIOS.Manager
{
    /// <summary>
    /// The Manager's "checksum a worker before running it" step: workers (and installed apps) live in a
    /// folder, and each is verified before launch so nobody slipped slop in.
    ///
    /// It Blake3-hashes the folder (via Notary) and compares it to an APPROVED baseline: a tree the
    /// Manager signed with its identity key when the user approved it (<c>XRUIOS.Manager baseline</c>,
    /// <c>install-app</c>). A folder with no signed baseline is not trusted on first sight any more; it
    /// stays down until it's approved. WalkerWorks, when it exists, slots in as another signer of these
    /// trees; until then the Manager is the authority.
    /// </summary>
    public sealed class NotaryVerifier
    {
        private readonly string _baselineDir;
        private readonly Dictionary<string, byte[]> _signPub;
        private readonly Dictionary<string, byte[]> _signPriv;

        public NotaryVerifier(string baselineDir, Dictionary<string, byte[]> signPublic, Dictionary<string, byte[]> signPrivate)
        {
            _baselineDir = baselineDir;
            _signPub = signPublic;
            _signPriv = signPrivate;
            Directory.CreateDirectory(_baselineDir);
        }

        private static string Safe(string name) => string.Concat(name.Split(Path.GetInvalidFileNameChars()));
        public string TreePath(string name) => Path.Combine(_baselineDir, Safe(name) + ".tree");
        private string SignaturePath(string name) => Path.Combine(_baselineDir, Safe(name) + ".sig");

        /// <summary>The signed baseline, or null (with the reason) when there isn't a valid one.</summary>
        private async Task<(Dictionary<string, byte[]>? tree, string? why)> LoadApprovedAsync(WorkerDescriptor worker)
        {
            string tree = TreePath(worker.Name), sig = SignaturePath(worker.Name);
            if (!File.Exists(tree) || !File.Exists(sig))
                return (null, "not approved yet - run `XRUIOS.Manager baseline`");

            var baseline = await KeyAndSign.LoadHugeTreeAsync(tree);
            if (!await BaselineSeal.VerifyAsync(_signPub, await File.ReadAllBytesAsync(sig), baseline))
                return (null, "baseline signature is invalid (edited, or signed by another Manager)");
            return (baseline, null);
        }

        /// <summary>True if the folder matches its signed baseline exactly.</summary>
        public async Task<bool> IsCleanAsync(WorkerDescriptor worker)
        {
            var (baseline, why) = await LoadApprovedAsync(worker);
            if (baseline is null)
            {
                Console.Error.WriteLine($"  [Notary] '{worker.Name}': {why}.");
                return false;
            }

            var current = await BaselineSeal.HashFolderAsync(worker.Folder);
            var diff = await KeyAndSign.CompareFileHashes(baseline, current);
            int changed = diff.Modified.Count + diff.Missing.Count + diff.Added.Count;
            if (changed == 0)
                return true;

            Console.Error.WriteLine(
                $"  [Notary] '{worker.Name}' FAILED checksum: {diff.Modified.Count} modified, " +
                $"{diff.Missing.Count} missing, {diff.Added.Count} added.");
            return false;
        }

        /// <summary>
        /// Approve the folder as it is right now: hash it, sign the tree, store both. Only called on an
        /// explicit user action (baseline / install-app), never automatically.
        /// </summary>
        public async Task<int> ApproveAsync(WorkerDescriptor worker)
        {
            var tree = await BaselineSeal.HashFolderAsync(worker.Folder);
            byte[] sig = await BaselineSeal.SignAsync(_signPriv, tree);
            await KeyAndSign.SaveHugeTreeAsync(TreePath(worker.Name), tree);
            await File.WriteAllBytesAsync(SignaturePath(worker.Name), sig);
            return tree.Count;
        }

        /// <summary>Forget an approval (uninstall).</summary>
        public void Revoke(string name)
        {
            foreach (var p in new[] { TreePath(name), SignaturePath(name) })
                if (File.Exists(p)) File.Delete(p);
        }

        /// <summary>Base64 signature to hand a worker alongside <see cref="TreePath"/>, or null if unapproved.</summary>
        public string? SignatureBase64(string name)
        {
            string p = SignaturePath(name);
            return File.Exists(p) ? Convert.ToBase64String(File.ReadAllBytes(p)) : null;
        }
    }
}
