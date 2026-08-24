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

        public BrokerRouter(WorkerSupervisor supervisor, PermissionService permissions,
            Dictionary<string, byte[]> managerSigningKey)
        {
            _supervisor = supervisor;
            _permissions = permissions;
            _pool = new WorkerSessionPool(managerSigningKey);
        }

        public async Task<DiracResponse> RouteAsync(DiracRequest request, AeadChannel toApp, string appId)
        {
            string capability = request.FunctionName;

            // 1. Permission.
            if (!await _permissions.CheckAsync(appId, capability))
            {
                Console.WriteLine($"[Broker] DENY {appId} -> {capability}");
                return new DiracResponse(false, Array.Empty<byte>(),
                    $"XRUIOS.Permission denied '{capability}' for {appId}.");
            }

            // 2. Route by capability (Manager knows which worker offers what).
            var worker = _supervisor.Registry.FirstOrDefault(w => w.Announcement.Capabilities.Contains(capability));
            if (worker == null)
                return new DiracResponse(false, Array.Empty<byte>(), $"No worker exposes '{capability}'.");

            Console.WriteLine($"[Broker] ALLOW {appId} -> {capability} @ {worker.Announcement.Name}");

            // 3. Relay over a WARM pooled session to the worker (no per-call re-handshake).
            var args = request.Parameters.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
            byte[] resultBytes = await _pool.InvokeAsync(worker, capability, args);

            // 4. Re-encrypt the worker's exact result onto the app's channel.
            var env = toApp.Encrypt(capability, resultBytes);
            return new DiracResponse(true, MessagePackSerializer.Serialize(env), "Success");
        }

        public ValueTask DisposeAsync() => _pool.DisposeAsync();
    }
}
