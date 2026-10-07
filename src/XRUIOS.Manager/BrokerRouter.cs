using EclipseLCL;
using MessagePack;
using XRUIOS.Interfaces;
using static EclipseProject.Security;

namespace XRUIOS.Manager
{
    /// <summary>
    /// The heart of "the Manager sends data between applications". Every app call lands here:
    ///   1. Check XRUIOS.Permission for (appId, capability) — deny → refuse.
    ///   2. Find the registered worker that exposes the capability (the Manager knows locations).
    ///   3. Relay to that worker over its Manager-only PSK channel and get the result bytes.
    ///   4. Re-encrypt those exact bytes onto the app's channel and return them.
    /// The app never touches the worker; the worker never sees the app. Both hops are encrypted.
    /// </summary>
    public sealed class BrokerRouter : IAsyncDisposable
    {
        private readonly WorkerSupervisor _supervisor;
        private readonly PermissionService _permissions;
        private readonly WorkerSessionPool _pool;
        private readonly EventHub _events;
        private readonly AuditTrail? _audit;

        public BrokerRouter(WorkerSupervisor supervisor, PermissionService permissions,
            Dictionary<string, byte[]> managerSigningKey, EventHub events, AuditTrail? audit = null)
        {
            _audit = audit;
            _supervisor = supervisor;
            _permissions = permissions;
            _pool = new WorkerSessionPool(managerSigningKey);
            _events = events;
        }

        public async Task<DiracResponse> RouteAsync(DiracRequest request, AeadChannel toApp, string clientId)
        {
            string capability = request.FunctionName;
            // An app's event stream is a second session "<appId>#events" on the same key: same app, same grants.
            string appId = XruiosEventProtocol.BaseIdentity(clientId);

            // 0. Events are served by the Manager itself, filtered per event by "Events.<Name>" grants.
            if (capability == XruiosEventProtocol.PollCapability)
                return await PollEventsAsync(request, toApp, appId);

            // 1. Permission. If the store itself fails (it rate-limits too), say so plainly instead of letting
            //    the exception escape as an opaque handler error.
            bool allowed;
            try { allowed = await _permissions.CheckAsync(appId, capability); }
            catch (Exception ex)
            {
                _audit?.Append(appId, "deny", capability, ok: false, "permission check failed: " + ex.GetType().Name);
                return new DiracResponse(false, Array.Empty<byte>(),
                    $"The permission check for '{capability}' could not be completed ({ex.Message}) Try again shortly.");
            }
            if (!allowed)
            {
                Console.WriteLine($"[Broker] DENY {appId} -> {capability}");
                _audit?.Append(appId, "deny", capability, ok: false);
                return new DiracResponse(false, Array.Empty<byte>(),
                    $"XRUIOS.Permission denied '{capability}' for {appId}.");
            }

            // 2. Route by capability (Manager knows which worker offers what).
            var worker = _supervisor.Registry.FirstOrDefault(w => w.Announcement.Capabilities.Contains(capability));
            if (worker == null)
            {
                _audit?.Append(appId, "allow", capability, ok: false, "no worker exposes it");
                return new DiracResponse(false, Array.Empty<byte>(), $"No worker exposes '{capability}'.");
            }

            Console.WriteLine($"[Broker] ALLOW {appId} -> {capability} @ {worker.Announcement.Name}");

            // 3. Relay over a WARM pooled session to the worker (no per-call re-handshake).
            // The app can't set call metadata itself; anything it sent under the reserved prefix is dropped
            // and the Manager stamps who actually asked, for the worker's audit trail.
            var args = request.Parameters
                .Where(kv => !kv.Key.StartsWith(WorkerOcean.MetaPrefix, StringComparison.Ordinal))
                .ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
            args[WorkerOcean.CallerKey] = appId;

            byte[] resultBytes;
            try { resultBytes = await _pool.InvokeAsync(worker, capability, args); }
            catch (Exception ex)
            {
                // Hand the worker's own message back as a normal failure. Rethrowing would surface in the
                // app as an opaque gRPC "Exception was thrown by handler".
                _audit?.Append(appId, "allow", capability, ok: false, $"{worker.Announcement.Name}: {ex.Message}");
                return new DiracResponse(false, Array.Empty<byte>(), ex.Message);
            }
            _audit?.Append(appId, "allow", capability, ok: true, worker.Announcement.Name);

            // 4. Re-encrypt the worker's exact result onto the app's channel.
            var env = toApp.Encrypt(capability, resultBytes);
            return new DiracResponse(true, MessagePackSerializer.Serialize(env), "Success");
        }

        private async Task<DiracResponse> PollEventsAsync(DiracRequest request, AeadChannel toApp, string appId)
        {
            long after = Long(request.Parameters, "after", -1);
            int waitMs = (int)Long(request.Parameters, "waitMs", 0);

            var batch = await _events.PollAsync(after, waitMs, async name =>
            {
                try { return await _permissions.CheckAsync(appId, XruiosEventProtocol.PermissionFor(name)); }
                catch { return false; }   // a failed check means "not now"; the app polls again
            });

            byte[] result = MessagePackSerializer.Serialize(batch.ToJson());
            var env = toApp.Encrypt(XruiosEventProtocol.PollCapability, result);
            return new DiracResponse(true, MessagePackSerializer.Serialize(env), "Success");
        }

        /// <summary>
        /// Record every running worker's audit head ("seq|mac") in the Manager's own log. `audit` later
        /// checks each worker log still contains that entry, which catches entries cut off the end.
        /// </summary>
        public async Task AnchorAuditAsync()
        {
            if (_audit is null) return;
            foreach (var worker in _supervisor.Registry)
            {
                try
                {
                    byte[] raw = await _pool.InvokeAsync(worker, WorkerOcean.AuditHeadCapability, new());
                    string head = MessagePackSerializer.Deserialize<string>(raw);
                    if (!head.StartsWith("0|")) _audit.Append("manager", "anchor", worker.Announcement.Name, ok: true, head);
                }
                catch (Exception ex)
                {
                    _audit.Append("manager", "anchor", worker.Announcement.Name, ok: false, ex.Message);
                }
            }
        }

        private static long Long(IReadOnlyDictionary<string, object?> args, string key, long fallback) =>
            args.TryGetValue(key, out var v) && v != null ? Convert.ToInt64(v) : fallback;

        public ValueTask DisposeAsync() => _pool.DisposeAsync();
    }
}
