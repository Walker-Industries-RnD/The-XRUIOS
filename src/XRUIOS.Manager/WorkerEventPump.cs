using System.Collections.Concurrent;
using XRUIOS.Interfaces;

namespace XRUIOS.Manager
{
    /// <summary>
    /// Pulls events out of every running worker into the <see cref="EventHub"/>. Each worker gets its
    /// own long-polling session - separate from the broker's pooled one, because a session carries one
    /// call at a time and a 20 s poll must never hold up real capability calls. Workers that start,
    /// restart (new address / new epoch) or stop are picked up by a reconcile tick.
    /// </summary>
    public sealed class WorkerEventPump : IAsyncDisposable
    {
        private const long PollWaitMs = 20_000;

        private readonly WorkerSupervisor _supervisor;
        private readonly EventHub _hub;
        private readonly Dictionary<string, byte[]> _managerSigningKey;
        private readonly ConcurrentDictionary<string, Task> _pumps = new(StringComparer.Ordinal);
        private readonly CancellationTokenSource _cts = new();
        private Task? _reconcile;

        public WorkerEventPump(WorkerSupervisor supervisor, EventHub hub, Dictionary<string, byte[]> managerSigningKey)
        {
            _supervisor = supervisor;
            _hub = hub;
            _managerSigningKey = managerSigningKey;
        }

        public void Start() => _reconcile ??= Task.Run(() => ReconcileAsync(_cts.Token));

        private async Task ReconcileAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                foreach (var worker in _supervisor.Registry)
                {
                    string name = worker.Announcement.Name;
                    if (_pumps.TryGetValue(name, out var running) && !running.IsCompleted) continue;
                    _pumps[name] = Task.Run(() => PumpAsync(name, ct));
                }
                try { await Task.Delay(1000, ct); } catch (OperationCanceledException) { return; }
            }
        }

        private async Task PumpAsync(string workerName, CancellationToken ct)
        {
            EclipseSecureClient? client = null;
            string? address = null, epoch = null;
            long cursor = 0;

            try
            {
                while (!ct.IsCancellationRequested && _supervisor.TryGet(workerName, out var worker))
                {
                    try
                    {
                        if (client == null || address != worker.Announcement.Address)
                        {
                            if (client != null) await client.DisposeAsync();
                            client = await EclipseSecureClient.ConnectAsync(worker.Announcement.Address, "xruios-manager-events",
                                worker.Psk, managerSigningKey: _managerSigningKey);
                            address = worker.Announcement.Address;
                        }

                        byte[] raw = await client.InvokeRawAsync(XruiosEventProtocol.WorkerPollCapability,
                            new() { ["epoch"] = epoch, ["after"] = cursor, ["waitMs"] = PollWaitMs });
                        var batch = XruiosEventBatch.FromJson(MessagePack.MessagePackSerializer.Deserialize<string>(raw));

                        if (batch.Reset)
                            Console.WriteLine($"[Events] '{workerName}' dropped events before the Manager read them.");
                        foreach (var e in batch.Events) _hub.Append(e);
                        epoch = batch.Epoch;
                        cursor = batch.Cursor;
                    }
                    catch (Exception) when (!ct.IsCancellationRequested)
                    {
                        // Worker restarting / session expired: drop the session and try again shortly.
                        if (client != null) { try { await client.DisposeAsync(); } catch { } client = null; }
                        try { await Task.Delay(1000, ct); } catch (OperationCanceledException) { break; }
                    }
                }
            }
            finally
            {
                if (client != null) { try { await client.DisposeAsync(); } catch { } }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            // In-flight polls can't be interrupted; they end within PollWaitMs. Don't hold shutdown for them.
            var all = _pumps.Values.Append(_reconcile ?? Task.CompletedTask);
            await Task.WhenAny(Task.WhenAll(all), Task.Delay(2000));
        }
    }
}
