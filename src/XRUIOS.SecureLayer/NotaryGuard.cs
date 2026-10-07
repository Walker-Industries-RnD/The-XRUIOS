using NotaryCore;
using NotaryShared;

namespace XRUIOS.Interfaces
{
    /// <summary>
    /// Notary-backed anti-tamper for a worker's on-disk footprint.
    ///
    /// Before the worker serves anything - and every 30 s afterward - every file in its install folder
    /// is Blake3-hashed and compared against a baseline tree. Any Modified/Missing/Added file means
    /// someone swapped a path underneath us, so we exit non-zero and let the Manager restart us.
    ///
    /// Trust model: the baseline is the Manager's opinion and nothing else. The Manager hands the worker
    /// the path of its approved tree plus the Manager's signature over it (<see cref="BaselineSeal"/>);
    /// the worker checks that signature against the pinned Manager public key and keeps the verified
    /// tree in memory. A tree the Manager didn't sign is never trusted. Only an explicit dev run
    /// (XRUIOS_DEV_STANDALONE=1, no Manager) falls back to capturing its own baseline.
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

        // The Manager-signed tree, verified once and then held in memory (never re-read from disk).
        private Dictionary<string, byte[]>? _trusted;

        /// <summary>
        /// Adopt the baseline the Manager signed for this worker (paths + signature arrive in the
        /// environment at launch). False if it's missing or the signature doesn't verify against the
        /// pinned Manager key - the caller must then refuse to run.
        /// </summary>
        public async Task<bool> AdoptSignedBaselineAsync(Dictionary<string, byte[]> managerPublicKey)
        {
            string? treePath = Environment.GetEnvironmentVariable(BaselineSeal.TreeEnv);
            string? sigB64 = Environment.GetEnvironmentVariable(BaselineSeal.SignatureEnv);
            if (string.IsNullOrEmpty(treePath) || string.IsNullOrEmpty(sigB64) || !File.Exists(treePath))
            {
                Console.Error.WriteLine("[NotaryGuard] No Manager-signed baseline was provided.");
                return false;
            }

            var tree = await KeyAndSign.LoadHugeTreeAsync(treePath);
            if (!await BaselineSeal.VerifyAsync(managerPublicKey, Convert.FromBase64String(sigB64), tree))
            {
                Console.Error.WriteLine("[NotaryGuard] Baseline signature does not verify against the Manager key.");
                return false;
            }

            _trusted = tree;
            Console.WriteLine($"[NotaryGuard] Manager-signed baseline verified ({tree.Count} files).");
            return true;
        }

        /// <summary>Dev standalone only: create a local baseline on first run (no Manager to ask).</summary>
        public async Task EnsureBaselineAsync()
        {
            if (File.Exists(_baselinePath))
                return;

            var tree = await BaselineSeal.HashFolderAsync(_folder);
            await KeyAndSign.SaveHugeTreeAsync(_baselinePath, tree);
            Console.WriteLine($"[NotaryGuard] DEV: captured local baseline ({tree.Count} files) -> {_baselinePath}");
        }

        /// <summary>Hash the folder now and diff it against the baseline.</summary>
        public async Task<Definitions.FileHashResults> VerifyAsync()
        {
            var baseline = _trusted ?? await KeyAndSign.LoadHugeTreeAsync(_baselinePath);
            var current = await BaselineSeal.HashFolderAsync(_folder);
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
