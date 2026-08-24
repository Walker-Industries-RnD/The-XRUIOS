using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using XRUIOS.Interfaces;

namespace XRUIOS.Manager
{
    /// <summary>
    /// The Manager's worker supervisor: checksums each worker, launches it, learns its
    /// address + capabilities + PUBLIC key, and keeps it alive — restarting it if it exits or
    /// trips its own tamper guard. Stopping is intentional and suppresses the restart.
    /// </summary>
    public sealed class WorkerSupervisor
    {
        public sealed class RegisteredWorker
        {
            public required WorkerDescriptor Descriptor { get; init; }
            public required Process Process { get; set; }
            public required WorkerAnnouncement Announcement { get; set; }
            /// <summary>The per-worker PSK the Manager generated. Manager-held; never leaves this process.</summary>
            public required byte[] Psk { get; init; }
            public bool StopRequested { get; set; }
            public int Restarts { get; set; }
        }

        private readonly NotaryVerifier _notary;
        private readonly string _managerPublicKeyB64;
        private readonly string _dataPath;
        private readonly string _publicDataPath;
        private readonly Func<string, byte[]> _workerKeyProvider;
        private readonly ConcurrentDictionary<string, RegisteredWorker> _registry = new(StringComparer.Ordinal);
        private const int MaxRestarts = 3;

        public WorkerSupervisor(NotaryVerifier notary, string managerPublicKeyB64,
            string dataPath, string publicDataPath, Func<string, byte[]> workerKeyProvider)
        {
            _notary = notary;
            _managerPublicKeyB64 = managerPublicKeyB64;
            _dataPath = dataPath;
            _publicDataPath = publicDataPath;
            _workerKeyProvider = workerKeyProvider;
        }

        public IReadOnlyCollection<RegisteredWorker> Registry => _registry.Values.ToArray();

        public bool TryGet(string name, out RegisteredWorker worker) => _registry.TryGetValue(name, out worker!);

        /// <summary>Checksum, launch, and register a worker. Returns false if it won't come up cleanly.</summary>
        public async Task<bool> StartAsync(WorkerDescriptor worker)
        {
            Console.WriteLine($"[Manager] Starting '{worker.Name}'...");

            // 1. Never run a tampered worker.
            if (!await _notary.IsCleanAsync(worker))
            {
                Console.Error.WriteLine($"[Manager] Refusing to launch '{worker.Name}' — checksum failed.");
                return false;
            }
            Console.WriteLine($"  [Notary] '{worker.Name}' checksum OK.");

            // Generate this worker's access password. The Manager keeps it; only a peer holding it
            // can handshake with the worker — so the worker talks to the Manager and nobody else.
            byte[] psk = RandomNumberGenerator.GetBytes(32);

            var announcement = await LaunchAndAwaitAnnouncementAsync(worker, psk);
            if (announcement == null)
                return false;

            var process = Process.GetProcessById(announcement.ProcessId);
            var reg = new RegisteredWorker { Descriptor = worker, Process = process, Announcement = announcement, Psk = psk };
            _registry[worker.Name] = reg;
            HookExit(reg);

            Console.WriteLine($"  [Manager] '{worker.Name}' up at {announcement.Address} (pid {announcement.ProcessId}); " +
                              $"caps=[{string.Join(", ", announcement.Capabilities)}]; " +
                              $"pubkey={PublicKeyFingerprint(announcement.PublicKey)}");
            return true;
        }

        /// <summary>Intentionally stop a worker (no restart).</summary>
        public void Stop(string name)
        {
            if (!_registry.TryGetValue(name, out var reg))
                return;

            reg.StopRequested = true;
            try
            {
                if (!reg.Process.HasExited)
                {
                    reg.Process.Kill(entireProcessTree: true);
                    reg.Process.WaitForExit(5000);
                }
            }
            catch { /* already gone */ }

            _registry.TryRemove(name, out _);
            Console.WriteLine($"[Manager] Stopped '{name}'.");
        }

        public void StopAll()
        {
            foreach (var name in _registry.Keys.ToArray())
                Stop(name);
        }

        private async Task<WorkerAnnouncement?> LaunchAndAwaitAnnouncementAsync(WorkerDescriptor worker, byte[] psk)
        {
            var psi = new ProcessStartInfo
            {
                FileName = worker.ExecutablePath,
                WorkingDirectory = worker.Folder,
                UseShellExecute = false
            };
            // Tell the worker the Manager already verified it (skip its self-check exit path)...
            psi.Environment["XRUIOS_MANAGER_MANAGED"] = "1";
            // ...and hand it its access secret out-of-band. This env var is the only place the PSK
            // is shared, parent→child, and never touches disk or the announcement.
            psi.Environment["XRUIOS_WORKER_PSK"] = Convert.ToBase64String(psk);
            // Pin our PUBLIC identity key so the worker can verify our handshake signature.
            psi.Environment["XRUIOS_MANAGER_PUBKEY"] = _managerPublicKeyB64;
            // Hand the worker its context out-of-band: the canonical paths and its OWN encryption key,
            // derived from the master. A worker only ever receives its own key, so a breach of one
            // can't read another's store. The worker binds these before any of its code runs.
            psi.Environment["XRUIOS_DATA_PATH"] = _dataPath;
            psi.Environment["XRUIOS_PUBLIC_PATH"] = _publicDataPath;
            psi.Environment["XRUIOS_WORKER_KEY"] = Convert.ToBase64String(_workerKeyProvider(worker.Name));

            var process = Process.Start(psi);
            if (process == null)
            {
                Console.Error.WriteLine($"[Manager] Failed to start process for '{worker.Name}'.");
                return null;
            }

            // Wait for a FRESH announcement — one whose pid matches the process we just launched
            // (guards against reading a stale announcement from a previous run).
            string storeKey = WorkerAnnouncement.StoreKey(worker.Name);
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                if (process.HasExited)
                {
                    Console.Error.WriteLine($"[Manager] '{worker.Name}' exited during startup (code {process.ExitCode}).");
                    return null;
                }

                var ann = Utils.SecureStore.Get<WorkerAnnouncement>(storeKey);
                if (ann != null && ann.ProcessId == process.Id)
                    return ann;

                await Task.Delay(100);
            }

            Console.Error.WriteLine($"[Manager] '{worker.Name}' did not announce within timeout.");
            try { if (!process.HasExited) process.Kill(true); } catch { }
            return null;
        }

        private void HookExit(RegisteredWorker reg)
        {
            reg.Process.EnableRaisingEvents = true;
            reg.Process.Exited += async (_, _) =>
            {
                if (reg.StopRequested)
                    return; // intentional

                int code = SafeExitCode(reg.Process);
                Console.Error.WriteLine($"[Manager] '{reg.Descriptor.Name}' exited unexpectedly (code {code}).");

                if (code == NotaryGuard.TamperExitCode)
                    Console.Error.WriteLine($"  [Manager] Exit was a TAMPER trip — re-verifying before restart.");

                if (reg.Restarts >= MaxRestarts)
                {
                    Console.Error.WriteLine($"  [Manager] '{reg.Descriptor.Name}' hit max restarts ({MaxRestarts}); leaving it down.");
                    _registry.TryRemove(reg.Descriptor.Name, out _);
                    return;
                }

                await Task.Delay(500);

                // Re-checksum, then relaunch. A restart rebinds a NEW ephemeral port and republishes,
                // so an attacker who swapped a path just gets cut off and replaced.
                if (!await _notary.IsCleanAsync(reg.Descriptor))
                {
                    Console.Error.WriteLine($"  [Manager] '{reg.Descriptor.Name}' still failing checksum; not restarting.");
                    _registry.TryRemove(reg.Descriptor.Name, out _);
                    return;
                }

                var ann = await LaunchAndAwaitAnnouncementAsync(reg.Descriptor, reg.Psk);
                if (ann == null)
                {
                    _registry.TryRemove(reg.Descriptor.Name, out _);
                    return;
                }

                reg.Process = Process.GetProcessById(ann.ProcessId);
                reg.Announcement = ann;
                reg.Restarts++;
                HookExit(reg);
                Console.WriteLine($"  [Manager] Restarted '{reg.Descriptor.Name}' at {ann.Address} (restart #{reg.Restarts}).");
            };
        }

        private static int SafeExitCode(Process p)
        {
            try { return p.ExitCode; } catch { return -1; }
        }

        /// <summary>Short SHA-256 fingerprint of a worker's public key, for at-a-glance identity.</summary>
        public static string PublicKeyFingerprint(Dictionary<string, byte[]> publicKey)
        {
            using var sha = SHA256.Create();
            foreach (var kv in publicKey.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                sha.TransformBlock(System.Text.Encoding.UTF8.GetBytes(kv.Key), 0, kv.Key.Length, null, 0);
                sha.TransformBlock(kv.Value, 0, kv.Value.Length, null, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha.Hash!)[..16].ToLowerInvariant();
        }
    }
}
