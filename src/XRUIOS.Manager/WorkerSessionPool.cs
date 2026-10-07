using System.Collections.Concurrent;
using XRUIOS.Interfaces;

namespace XRUIOS.Manager
{
    /// <summary>
    /// Keeps warm Eclipse sessions to each worker so the broker stops re-handshaking on every call
    /// (that handshake - enroll + Kyber + Dilithium sign/verify - was the ~14ms per-call cost).
    ///
    /// Concurrency: Eclipse's AeadChannel enforces strict monotonic per-message sequence numbers, so
    /// one session never has two invokes in flight. Each worker gets a small pool instead (up to
    /// <see cref="MaxSessionsPerWorker"/>): a call borrows an idle session or opens a new one, so a slow
    /// capability (an assistant waiting for its answer, a power request's grace period) doesn't hold up
    /// every other call to that worker.
    ///
    /// Resilience: if the worker rebound to a new address (restart) or the session died (idle-expired
    /// server-side, seq desync), we transparently re-handshake and retry once - but NOT on an
    /// application-level failure, so a non-idempotent capability isn't silently run twice.
    /// </summary>
    public sealed class WorkerSessionPool : IAsyncDisposable
    {
        public const int MaxSessionsPerWorker = 4;

        private sealed record Session(EclipseSecureClient Client, string Address);

        private sealed class Entry
        {
            public readonly SemaphoreSlim Slots = new(MaxSessionsPerWorker, MaxSessionsPerWorker);
            public readonly ConcurrentBag<Session> Idle = new();
        }

        private readonly ConcurrentDictionary<string, Entry> _byWorker = new(StringComparer.Ordinal);
        private readonly Dictionary<string, byte[]> _managerSigningKey;

        public WorkerSessionPool(Dictionary<string, byte[]> managerSigningKey)
            => _managerSigningKey = managerSigningKey;

        public async Task<byte[]> InvokeAsync(
            WorkerSupervisor.RegisteredWorker worker, string capability, Dictionary<string, object?> args)
        {
            var entry = _byWorker.GetOrAdd(worker.Announcement.Name, _ => new Entry());
            await entry.Slots.WaitAsync();
            Session? session = null;
            try
            {
                session = await BorrowAsync(entry, worker);
                try
                {
                    return await session.Client.InvokeRawAsync(capability, args);
                }
                // Transport/session failure (e.g. "Session not found") - reconnect + retry once.
                // App-level failures from InvokeRawAsync are prefixed "Worker call failed:" and must
                // NOT be retried (they may have executed a side effect already).
                catch (Exception ex) when (!IsAppFailure(ex))
                {
                    var stale = session;
                    session = null;
                    await DropAsync(stale);
                    session = await ConnectAsync(worker);
                    return await session.Client.InvokeRawAsync(capability, args);
                }
            }
            catch (Exception ex) when (session != null && !IsAppFailure(ex))
            {
                // The retry's session failed at the transport level too; don't hand it out again.
                await DropAsync(session);
                session = null;
                throw;
            }
            finally
            {
                if (session != null) entry.Idle.Add(session);
                entry.Slots.Release();
            }
        }

        private static bool IsAppFailure(Exception ex) =>
            ex.Message.StartsWith("Worker call failed:", StringComparison.Ordinal);

        // An idle session to the worker's CURRENT address, or a fresh one. Sessions to an old address
        // (the worker restarted) are closed on the way.
        private async Task<Session> BorrowAsync(Entry entry, WorkerSupervisor.RegisteredWorker worker)
        {
            while (entry.Idle.TryTake(out var s))
            {
                if (s.Address == worker.Announcement.Address) return s;
                await DropAsync(s);
            }
            return await ConnectAsync(worker);
        }

        private async Task<Session> ConnectAsync(WorkerSupervisor.RegisteredWorker worker)
        {
            var client = await EclipseSecureClient.ConnectAsync(
                worker.Announcement.Address, "xruios-manager", worker.Psk,
                managerSigningKey: _managerSigningKey);
            return new Session(client, worker.Announcement.Address);
        }

        private static async Task DropAsync(Session s)
        {
            try { await s.Client.DisposeAsync(); } catch { /* already gone */ }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var e in _byWorker.Values)
                while (e.Idle.TryTake(out var s))
                    await DropAsync(s);
            _byWorker.Clear();
        }
    }
}
