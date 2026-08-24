using System.Collections.Concurrent;
using XRUIOS.Interfaces;

namespace XRUIOS.Manager
{
    /// <summary>
    /// Keeps ONE warm Eclipse session per worker so the broker stops re-handshaking on every call
    /// (that handshake — enroll + Kyber + Dilithium sign/verify — was the ~14ms per-call cost).
    ///
    /// Concurrency: Eclipse's AeadChannel enforces strict monotonic per-message sequence numbers, so
    /// a single session must never have two invokes in flight at once. Each worker's session is
    /// therefore serialized by a gate; DIFFERENT workers still run fully in parallel. (To make one hot
    /// worker concurrent you'd hold a small pool of sessions to it — one is plenty for now.)
    ///
    /// Resilience: if the worker rebound to a new address (restart) or the session died (idle-expired
    /// server-side, seq desync), we transparently re-handshake and retry once — but NOT on an
    /// application-level failure, so a non-idempotent capability isn't silently run twice.
    /// </summary>
    public sealed class WorkerSessionPool : IAsyncDisposable
    {
        private sealed class Entry
        {
            public readonly SemaphoreSlim Gate = new(1, 1);
            public EclipseSecureClient? Client;
            public string? Address;
        }

        private readonly ConcurrentDictionary<string, Entry> _byWorker = new(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> _managerSigningKey;

        public WorkerSessionPool(Dictionary<string, byte[]> managerSigningKey)
            => _managerSigningKey = managerSigningKey;

        public async Task<byte[]> InvokeAsync(
            WorkerSupervisor.RegisteredWorker worker, string capability, Dictionary<string, object?> args)
        {
            var entry = _byWorker.GetOrAdd(worker.Announcement.Name, _ => new Entry());
            await entry.Gate.WaitAsync();
            try
            {
                // Reconnect if we have no session or the worker rebound to a new address (restart).
                if (entry.Client == null || entry.Address != worker.Announcement.Address)
                    await Reconnect(entry, worker);

                try
                {
                    return await entry.Client!.InvokeRawAsync(capability, args);
                }
                // Transport/session failure (e.g. "Session not found") — reconnect + retry once.
                // App-level failures from InvokeRawAsync are prefixed "Worker call failed:" and must
                // NOT be retried (they may have executed a side effect already).
                catch (Exception ex) when (!ex.Message.StartsWith("Worker call failed:", StringComparison.Ordinal))
                {
                    await Reconnect(entry, worker);
                    return await entry.Client!.InvokeRawAsync(capability, args);
                }
            }
            finally
            {
                entry.Gate.Release();
            }
        }

        private async Task Reconnect(Entry entry, WorkerSupervisor.RegisteredWorker worker)
        {
            if (entry.Client != null)
            {
                try { await entry.Client.DisposeAsync(); } catch { /* already gone */ }
            }
            entry.Client = await EclipseSecureClient.ConnectAsync(
                worker.Announcement.Address, "xruios-manager", worker.Psk,
                managerSigningKey: _managerSigningKey);
            entry.Address = worker.Announcement.Address;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var e in _byWorker.Values)
                if (e.Client != null)
                {
                    try { await e.Client.DisposeAsync(); } catch { /* best effort */ }
                }
            _byWorker.Clear();
        }
    }
}
