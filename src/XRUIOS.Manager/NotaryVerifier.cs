using NotaryCore;
using NotaryShared;

namespace XRUIOS.Manager
{
    /// <summary>
    /// The Manager's "checksum a worker before running it" step — the heart of the original vision:
    /// workers live in a folder and are each verified before launch so nobody slipped slop in.
    ///
    /// It Blake3-hashes the worker's folder (via Notary) and compares it to a trusted baseline.
    /// In production that baseline is the signed checksum published on walkerworks (served by
    /// NotaryApp); the Manager downloads + verifies it, then diffs the on-disk tree. Here we keep a
    /// local baseline per worker (captured trust-on-first-use) so the flow runs offline — swap
    /// <see cref="LoadBaselineAsync"/> for the walkerworks fetch when wiring the real store.
    /// </summary>
    public sealed class NotaryVerifier
    {
        private readonly string _baselineDir;

        public NotaryVerifier(string baselineDir)
        {
            _baselineDir = baselineDir;
            Directory.CreateDirectory(_baselineDir);
        }

        private string BaselinePath(string workerName) =>
            Path.Combine(_baselineDir, string.Concat(workerName.Split(Path.GetInvalidFileNameChars())) + ".tree");

        /// <summary>
        /// TODO(walkerworks): replace with an authenticated download of the signed baseline from
        /// walkerworks + Notary signature verification. For now: trust-on-first-use locally.
        /// </summary>
        private async Task<Dictionary<string, byte[]>> LoadBaselineAsync(WorkerDescriptor worker)
        {
            string path = BaselinePath(worker.Name);
            if (File.Exists(path))
                return await KeyAndSign.LoadHugeTreeAsync(path);

            var tree = await KeyAndSign.ProcessDirectoryAsync(worker.Folder);
            await KeyAndSign.SaveHugeTreeAsync(path, tree);
            Console.WriteLine($"  [Notary] No baseline for '{worker.Name}' — captured {tree.Count} files (TOFU).");
            return tree;
        }

        /// <summary>Hash the worker folder now and diff against the trusted baseline.</summary>
        public async Task<Definitions.FileHashResults> VerifyAsync(WorkerDescriptor worker)
        {
            var baseline = await LoadBaselineAsync(worker);
            var current = await KeyAndSign.ProcessDirectoryAsync(worker.Folder);
            return await KeyAndSign.CompareFileHashes(baseline, current);
        }

        /// <summary>True if the worker folder matches its baseline exactly.</summary>
        public async Task<bool> IsCleanAsync(WorkerDescriptor worker)
        {
            var diff = await VerifyAsync(worker);
            int changed = diff.Modified.Count + diff.Missing.Count + diff.Added.Count;
            if (changed == 0)
                return true;

            Console.Error.WriteLine(
                $"  [Notary] '{worker.Name}' FAILED checksum: {diff.Modified.Count} modified, " +
                $"{diff.Missing.Count} missing, {diff.Added.Count} added.");
            return false;
        }

        /// <summary>Re-capture the baseline (e.g. after a legitimate update the Manager just applied).</summary>
        public async Task RecaptureBaselineAsync(WorkerDescriptor worker)
        {
            var tree = await KeyAndSign.ProcessDirectoryAsync(worker.Folder);
            await KeyAndSign.SaveHugeTreeAsync(BaselinePath(worker.Name), tree);
            Console.WriteLine($"  [Notary] Re-captured baseline for '{worker.Name}' ({tree.Count} files).");
        }
    }
}
