using NotaryCore;
using NotaryShared;

namespace XRUIOS.Interfaces
{
    /// <summary>
    /// Notary-backed anti-tamper for a worker's on-disk footprint.
    ///
    /// Replaces the old Pariah self-hash (Worker.VerifyIntegrity/VerifyIntegrity2). Before the
    /// worker serves anything — and periodically afterward — every file in its install folder is
    /// Blake3-hashed and compared against a baseline tree. Any Modified/Missing/Added file means
    /// someone swapped a path underneath us, so we exit non-zero and let the Manager restart us.
    ///
    /// Trust model / seam: in production the baseline is a Manager-signed manifest, decrypted with
    /// the per-worker key the Manager hands down (NotaryCore.Signing.DecryptAppHostKeyPair) and
    /// verified with CheckForCertificateCredibility. Here we accept a plain baseline path and, on
    /// first run, capture the current tree as the baseline (trust-on-first-use) so the worker is
    /// runnable standalone. Wire the Manager key + ValidationCertificate in at the marked TODO.
    /// </summary>
    public sealed class NotaryGuard
    {
        private readonly string _folder;
        private readonly string _baselinePath;

        // Exit code the Manager watches for to mean "worker integrity failed, restart me".
        public const int TamperExitCode = 44;

        /// <param name="folder">The worker's install directory to protect (e.g. AppContext.BaseDirectory).</param>
        /// <param name="baselinePath">Where the signed baseline tree lives. Kept OUTSIDE <paramref name="folder"/>.</param>
        public NotaryGuard(string folder, string baselinePath)
        {
            _folder = folder;
            _baselinePath = baselinePath;
        }

        /// <summary>
        /// Convenience factory: protects the running assembly's folder, storing the baseline in a
        /// per-worker location outside that folder.
        /// </summary>
        public static NotaryGuard ForCurrentWorker(string serverName)
        {
            string folder = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            string baseDir = Path.Combine(Path.GetTempPath(), "xruios_notary");
            Directory.CreateDirectory(baseDir);
            string safeName = string.Concat(serverName.Split(Path.GetInvalidFileNameChars()));
            return new NotaryGuard(folder, Path.Combine(baseDir, safeName + ".tree"));
        }

        /// <summary>
        /// Overwrite the baseline with the folder's current state. Used when the Manager has already
        /// checksum-verified us before launch: we trust that verdict and re-baseline so the periodic
        /// watch detects any change from this launch point onward (rather than exiting on a stale tree).
        /// </summary>
        public async Task CaptureFreshBaselineAsync()
        {
            var tree = await KeyAndSign.ProcessDirectoryAsync(_folder);
            await KeyAndSign.SaveHugeTreeAsync(_baselinePath, tree);
            Console.WriteLine($"[NotaryGuard] Manager-managed: captured fresh runtime baseline ({tree.Count} files).");
        }

        /// <summary>Create the baseline on first run if the Manager hasn't provisioned one.</summary>
        public async Task EnsureBaselineAsync()
        {
            if (File.Exists(_baselinePath))
                return;

            // TODO(Manager): replace trust-on-first-use with a Manager-signed baseline,
            // decrypted via Signing.DecryptAppHostKeyPair(managerKey) and verified with
            // KeyAndSign.CheckForCertificateCredibility before it is trusted.
            var tree = await KeyAndSign.ProcessDirectoryAsync(_folder);
            await KeyAndSign.SaveHugeTreeAsync(_baselinePath, tree);
            Console.WriteLine($"[NotaryGuard] Captured baseline ({tree.Count} files) -> {_baselinePath}");
        }

        /// <summary>Hash the folder now and diff it against the baseline.</summary>
        public async Task<Definitions.FileHashResults> VerifyAsync()
        {
            var baseline = await KeyAndSign.LoadHugeTreeAsync(_baselinePath);
            var current = await KeyAndSign.ProcessDirectoryAsync(_folder);
            return await KeyAndSign.CompareFileHashes(baseline, current);
        }

        /// <summary>Verify and, on any tamper signal, log and terminate the process.</summary>
        public async Task VerifyOrExitAsync(string stage)
        {
            var diff = await VerifyAsync();
            int changed = diff.Modified.Count + diff.Missing.Count + diff.Added.Count;
            if (changed == 0)
            {
                Console.WriteLine($"[NotaryGuard] {stage} verification passed.");
                return;
            }

            Console.Error.WriteLine(
                $"[NotaryGuard] TAMPER DETECTED at {stage}: " +
                $"{diff.Modified.Count} modified, {diff.Missing.Count} missing, {diff.Added.Count} added. Exiting.");
            Environment.Exit(TamperExitCode);
        }

        /// <summary>Background loop that re-verifies on an interval and exits on tamper.</summary>
        public void StartPeriodic(TimeSpan interval, CancellationToken token)
        {
            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(interval, token);
                        await VerifyOrExitAsync("periodic");
                    }
                    catch (TaskCanceledException) { break; }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[NotaryGuard] periodic check error: {ex.Message}");
                    }
                }
            }, token);
        }
    }
}
