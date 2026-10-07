using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using EclipseLCL;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using EclipseProject;
using WISecureData;
using static EclipseProject.Security;
using XRUIOS.Interpreter;
using static Pariah_Cybersecurity.EasyPQC;

namespace XRUIOS.Interfaces
{
    /// <summary>
    /// Hosts a Plagues worker behind Eclipse's encrypted channel.
    ///
    /// Replaces the old MagicOnion IPublicAcc host + Pariah self-hash handshake. A worker
    /// now: (1) proves it hasn't been tampered with via <see cref="NotaryGuard"/>, (2) stands
    /// up a MagicOnion <see cref="PlaguesDiracService"/> that speaks Eclipse's
    /// enroll → handshake → encrypted-invoke protocol, and (3) publishes its bound
    /// (ephemeral) address to <see cref="Utils.SecureStore"/> so clients/the Manager can find it.
    /// </summary>
    public static class SecureWorkerHost
    {
        // Set once at Run(); read by the per-request PlaguesDiracService instances.
        internal static (Dictionary<string, byte[]> Public, Dictionary<string, byte[]> Private) ServerKyberKeys;
        internal static WorkerOcean? Ocean;
        internal static IPermissionGate Gate = new ManagerOnlyGate();
        internal static bool Enabled;

        // The single pre-shared key that authenticates the ONLY peer allowed to talk to this
        // worker — the XRUIOS.Manager. The Manager generates it and hands it over at launch via
        // XRUIOS_WORKER_PSK; a caller that doesn't hold it cannot complete the handshake.
        internal static byte[] WorkerPsk = Array.Empty<byte>();

        // Pluggable so the SAME host serves both a worker (one Manager PSK) and the Manager broker
        // (a different PSK per app). Given a client's identity, return its PSK or null to reject.
        internal static Func<string, byte[]?> PskResolver = _ => WorkerPsk;

        // Pluggable invoke handler. Null ⇒ dispatch locally via the worker's WorkerOcean+Gate.
        // The Manager broker sets this to a router that permission-checks then relays to a worker.
        internal static Func<DiracRequest, AeadChannel, string, Task<DiracResponse>>? Router;

        // The Manager's PUBLIC identity key (set on a worker from env at launch). When present, the
        // worker cryptographically verifies its peer is the real Manager during enrollment.
        internal static Dictionary<string, byte[]>? ManagerPublicKey;

        // Client ids whose enrollment carried a valid Manager signature. The handshake refuses any
        // other id, and ManagerOnlyGate refuses calls on any other session - so holding the PSK alone
        // (without the Manager's private identity key) gets a caller nowhere.
        private static readonly ConcurrentDictionary<string, DateTime> VerifiedClients = new(StringComparer.Ordinal);

        internal static void MarkVerified(string clientId)
        {
            VerifiedClients[clientId] = DateTime.UtcNow;
            if (VerifiedClients.Count > 4096) // reconnect churn; drop anything older than a day
                foreach (var (id, at) in VerifiedClients)
                    if (at < DateTime.UtcNow.AddDays(-1)) VerifiedClients.TryRemove(id, out _);
        }

        internal static bool IsVerifiedClient(string clientId) => VerifiedClients.ContainsKey(clientId);
        internal static void ForgetClient(string clientId) => VerifiedClients.TryRemove(clientId, out _);

        /// <summary>True when this worker is locked to a Manager and the session was opened by it.</summary>
        public static bool IsVerifiedManagerSession(string clientId) =>
            ManagerPublicKey != null && VerifiedClients.ContainsKey(clientId);

        /// <summary>Set to 1 to boot a worker without a Manager (throwaway dev store, nobody can call it).</summary>
        public const string DevStandaloneEnv = "XRUIOS_DEV_STANDALONE";

        /// <summary>Exit code meaning "hardening tripped — restart me clean." Shared with NotaryGuard's intent.</summary>
        public const int TamperExit = 66;

        /// <summary>
        /// On-awake process hardening (Pariah AntiTamper): block extension-point DLL injection and
        /// start the debugger watchdog. We skip the watchdog if a debugger is already attached at
        /// startup, so developing under a debugger doesn't instakill the process — but a debugger
        /// that attaches LATER (the memory-scraping attack) still trips <paramref name="onDebuggerDetected"/>.
        /// </summary>
        public static void HardenProcess(Action onDebuggerDetected)
        {
            AntiTamper.TryHardenAgainstInjection();
            if (!AntiTamper.IsDebuggerAttached())
                AntiTamper.StartWatchdog(onDebuggerDetected, pollMilliseconds: 500);
        }

        /// <param name="serverName">SecureStore key under which the bound address is published.</param>
        /// <param name="capabilityAssembly">Assembly to scan for [SeaOfDirac] capabilities (usually the worker exe).</param>
        /// <param name="gate">Worker-side gate. Defaults to <see cref="ManagerOnlyGate"/>; per-app checks happen at the broker.</param>
        /// <param name="guard">Notary anti-tamper guard. Defaults to one over this worker's own folder.</param>
        /// <param name="unixSocketPath">Optional Unix socket to listen on (Linux); otherwise an ephemeral loopback port.</param>
        public static async Task Run(
            string serverName,
            Assembly capabilityAssembly,
            IPermissionGate? gate = null,
            NotaryGuard? guard = null,
            string? unixSocketPath = null)
        {
            gate ??= new ManagerOnlyGate();
            guard ??= NotaryGuard.ForCurrentWorker(serverName);

            // 0. Harden the process on awake: block DLL injection and start the Pariah watchdog.
            //    If a debugger attaches later (e.g. someone trying to scrape the PSK out of memory),
            //    the watchdog wipes our secrets and exits — the Manager then restarts a clean worker.
            HardenProcess(() =>
            {
                Console.Error.WriteLine($"[{serverName}] Debugger detected — wiping secrets, exiting.");
                CryptographicOperations.ZeroMemory(WorkerPsk);
                Environment.Exit(TamperExit);
            });

            bool managerManaged = Environment.GetEnvironmentVariable("XRUIOS_MANAGER_MANAGED") == "1";
            bool devStandalone = Environment.GetEnvironmentVariable(DevStandaloneEnv) == "1";
            if (!managerManaged && !devStandalone)
            {
                Console.Error.WriteLine($"[{serverName}] Workers are started by XRUIOS.Manager (`XRUIOS.Manager run`). " +
                                        $"Set {DevStandaloneEnv}=1 to boot it alone for debugging.");
                Environment.Exit(2);
            }

            // 1. Manager-provisioned access secret + the Manager's PUBLIC identity key (its private half
            //    never leaves the Manager). Under the Manager both are required: enrollment then demands
            //    a valid Manager signature on top of the shared PSK.
            string? pskB64 = Environment.GetEnvironmentVariable("XRUIOS_WORKER_PSK");
            string? mgrPubB64 = Environment.GetEnvironmentVariable("XRUIOS_MANAGER_PUBKEY");
            if (managerManaged)
            {
                if (string.IsNullOrEmpty(pskB64) || string.IsNullOrEmpty(mgrPubB64))
                {
                    Console.Error.WriteLine($"[{serverName}] Launched as Manager-managed but without its PSK / Manager key. Refusing.");
                    Environment.Exit(2);
                }
                WorkerPsk = Convert.FromBase64String(pskB64);
                ManagerPublicKey = MessagePack.MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(
                    Convert.FromBase64String(mgrPubB64));
                Console.WriteLine($"[{serverName}] Locked to Manager (PSK provisioned, identity key pinned).");
            }
            else
            {
                WorkerPsk = RandomNumberGenerator.GetBytes(32);
                Console.WriteLine($"[{serverName}] DEV STANDALONE: ephemeral secret, nobody can call this worker.");
            }

            // 2. Anti-tamper gate BEFORE we bind or serve anything. Under the Manager the only baseline
            //    we accept is the one the Manager signed; standalone dev runs capture their own.
            if (managerManaged)
            {
                if (!await guard.AdoptSignedBaselineAsync(ManagerPublicKey!))
                    Environment.Exit(NotaryGuard.TamperExitCode);
            }
            else
            {
                await guard.EnsureBaselineAsync();
            }
            await guard.VerifyOrExitAsync("startup");

            // Presentation shell. Under Aether the visual parts of the interpreter (toasts, desktop
            // appearance, scene apps) become events the engine renders; the rest stays on the OS.
            if (AetherShellInterpreter.IsRequested)
            {
                XruiosInterpreter.Use(new AetherShellInterpreter(XruiosInterpreter.Platform, WorkerEvents.Publish));
                Console.WriteLine($"[{serverName}] Shell: Aether (visual interpreter areas forwarded as events).");
            }

            // 3. Crypto + capability registry.
            ServerKyberKeys = Keys.Initiate();
            Ocean = new WorkerOcean(capabilityAssembly) { Audit = AuditTrail.ForWorker(serverName) };
            WorkerEvents.Source = serverName;
            Gate = gate;
            PskResolver = _ => WorkerPsk;   // one Manager peer
            Router = null;                  // dispatch locally via Ocean
            Enabled = true;

            Console.WriteLine($"[{serverName}] Capabilities: {string.Join(", ", Ocean.Capabilities)}");

            var builder = WebApplication.CreateBuilder();
            builder.Services.AddSingleton<SessionStore>();
            builder.Services.AddMagicOnion();

            builder.WebHost.ConfigureKestrel(options =>
            {
                if (unixSocketPath != null)
                    options.ListenUnixSocket(unixSocketPath, o => o.Protocols = HttpProtocols.Http2);
                else
                    options.Listen(System.Net.IPAddress.Loopback, 0, o => o.Protocols = HttpProtocols.Http2);
            });

            var app = builder.Build();

            // Register ONLY our service. Eclipse's own DiracService lives in the referenced
            // EclipseProject.dll and implements the same IDiracService; mapping explicitly
            // avoids a duplicate-registration collision.
            app.MapMagicOnionService(new[] { typeof(PlaguesDiracService) });

            await app.StartAsync();

            var server = app.Services.GetRequiredService<IServer>();
            var addressesFeature = server.Features.Get<IServerAddressesFeature>();
            while (addressesFeature == null || !addressesFeature.Addresses.Any())
            {
                await Task.Delay(50);
                addressesFeature = server.Features.Get<IServerAddressesFeature>();
            }

            string address = unixSocketPath != null
                ? $"http://unix:{unixSocketPath}"
                : addressesFeature.Addresses.First();

            Utils.SecureStore.Set(serverName, address);

            // Announce ourselves to the Manager: address + capabilities + PUBLIC key only.
            var announcement = new WorkerAnnouncement
            {
                Name = serverName,
                Address = address,
                PublicKey = ServerKyberKeys.Public,
                Capabilities = Ocean.Capabilities.ToArray(),
                ProcessId = Environment.ProcessId
            };
            Utils.SecureStore.Set(WorkerAnnouncement.StoreKey(serverName), announcement);

            Console.WriteLine($"[{serverName}] Bound at {address}; announced to Manager (pid {announcement.ProcessId}).");

            // 3. Periodic re-verification. On tamper the guard exits the process; the Manager
            //    restarts the worker, which rebinds a fresh port and republishes a new address —
            //    so an attacker who swapped a file just gets cut off and replaced.
            guard.StartPeriodic(TimeSpan.FromSeconds(30), CancellationToken.None);

            // Block until the process is asked to shut down (StartAsync only awaits startup).
            await app.WaitForShutdownAsync();
        }

        /// <summary>
        /// Host the XRUIOS.Manager's broker endpoint: same Eclipse protocol as a worker, but with a
        /// per-app PSK resolver and a custom router (permission-check + relay to a worker). No Notary
        /// guard — the Manager is the trusted core, not a checksummed worker. Returns the running app
        /// so the Manager can keep doing other work; call <see cref="ShutdownAsync"/> to stop it.
        /// </summary>
        public static async Task<WebApplication> RunBrokerAsync(
            string serverName,
            Func<string, byte[]?> pskResolver,
            Func<DiracRequest, AeadChannel, string, Task<DiracResponse>> router)
        {
            ServerKyberKeys = Keys.Initiate();
            PskResolver = pskResolver;
            Router = router;
            Gate = new RouterGate(); // gating happens inside the router (per app, per requested capability)
            Enabled = true;

            var builder = WebApplication.CreateBuilder();
            builder.Services.AddSingleton<SessionStore>();
            builder.Services.AddMagicOnion();
            builder.WebHost.ConfigureKestrel(options =>
                options.Listen(System.Net.IPAddress.Loopback, 0, o => o.Protocols = HttpProtocols.Http2));

            var app = builder.Build();
            app.MapMagicOnionService(new[] { typeof(PlaguesDiracService) });

            await app.StartAsync();

            var server = app.Services.GetRequiredService<IServer>();
            var addressesFeature = server.Features.Get<IServerAddressesFeature>();
            while (addressesFeature == null || !addressesFeature.Addresses.Any())
            {
                await Task.Delay(50);
                addressesFeature = server.Features.Get<IServerAddressesFeature>();
            }

            string address = addressesFeature.Addresses.First();
            Utils.SecureStore.Set(serverName, address);
            Console.WriteLine($"[{serverName}] Broker listening at {address}.");
            return app;
        }
    }

    // The broker's local gate: every call is handed to the router, which does the real per-app check.
    internal sealed class RouterGate : IPermissionGate
    {
        public bool Check(string requesterUuid, string capability) => true;
    }

    /// <summary>
    /// MagicOnion service implementing Eclipse's IDiracService. Faithful re-implementation of
    /// Eclipse's server handshake (EnrollAsync → BeginHandshakeAsync → FinishHandshakeAsync →
    /// InvokeAsync → FinishAsync), but dispatching through the worker's own <see cref="WorkerOcean"/>.
    /// </summary>
    public class PlaguesDiracService : ServiceBase<IDiracService>, IDiracService
    {
        private readonly SessionStore _sessions;

        public PlaguesDiracService(SessionStore sessions)
        {
            _sessions = sessions;
        }

        // Sessions expire after a while idle. A distinct gRPC status (instead of a generic handler
        // failure) tells the client it's safe to handshake again and retry: nothing was executed.
        internal static Grpc.Core.RpcException SessionLost() =>
            new(new Grpc.Core.Status(Grpc.Core.StatusCode.Unauthenticated, "Session not found."));

        public async UnaryResult<Dictionary<string, byte[]>> EnrollAsync(string clientName, string clientId)
        {
            // If this worker has the Manager's public identity key pinned, the caller must prove it's
            // the Manager by signing its clientId (the signature rides in place of the name). This is
            // asymmetric peer auth: a compromised worker holds only the PUBLIC key and can't forge it.
            var mgrPub = SecureWorkerHost.ManagerPublicKey;
            if (mgrPub != null)
            {
                byte[] sig;
                try { sig = Convert.FromBase64String(clientName); }
                catch { throw new Exception("Connection refused: missing Manager signature."); }

                if (!await PqcGate.RunAsync(() => Signatures.VerifySignature(mgrPub, sig, clientId)))
                    throw new Exception("Connection refused: caller is not the Manager (bad signature).");
                SecureWorkerHost.MarkVerified(clientId);
            }

            // The Kyber public key is fine to hand out; the real gate is the PSK-bound handshake.
            return SecureWorkerHost.ServerKyberKeys.Public;
        }

        public async UnaryResult<(byte[] nonceS, byte[] sessionId, uint epoch)> BeginHandshakeAsync(
            string clientId, byte[] cipher, byte[] nonceC)
        {
            byte[] nonceS = RandomNumberGenerator.GetBytes(16);
            byte[] sessionId = RandomNumberGenerator.GetBytes(8);
            uint epoch = 1;

            // Resolve the caller's PSK by identity. Workers return the one Manager PSK; the broker
            // returns the per-app PSK (or null → reject). Without the right PSK the transcript fails.
            // A worker locked to the Manager only handshakes ids whose enrollment was Manager-signed,
            // so the public key (also in the announcement) plus a leaked PSK isn't enough.
            if (SecureWorkerHost.ManagerPublicKey != null && !SecureWorkerHost.IsVerifiedClient(clientId))
                throw new Exception("Connection refused: enroll with a Manager signature first.");

            byte[] PSK = SecureWorkerHost.PskResolver(clientId)
                ?? throw new Exception("Connection refused: unknown client identity.");

            var keys = await PqcGate.RunAsync(() =>
            {
                byte[] sharedSecret = Keys.CreateSecretTwo(SecureWorkerHost.ServerKyberKeys.Private, cipher);
                return PrepareKeys(PSK, nonceC, nonceS, sharedSecret);
            });

            byte[] transcriptHash = SHA256.HashData(ByteArrayExtensions.Combine(
                Encoding.UTF8.GetBytes(clientId), cipher, nonceC, nonceS, sessionId, BitConverter.GetBytes(epoch)));

            _sessions.Upsert(new SessionState(
                clientId, sessionId, epoch,
                new AeadChannel(keys.k_c2s, sessionId, clientId, 1, new Transcript(transcriptHash, "client-finished")),
                new AeadChannel(keys.k_s2c, sessionId, clientId, 1, new Transcript(transcriptHash, "server-finished"))));

            return (nonceS, sessionId, epoch);
        }

        public async UnaryResult<byte[]> FinishHandshakeAsync(string clientId, byte[] clientTranscriptRaw)
        {
            if (!_sessions.TryGet(clientId, out SessionState s))
                throw SessionLost();

            var clientProof = s.FromClient.transcript.proof ?? throw new Exception("Connection refused: no client proof.");
            var serverProof = s.ToClient.transcript.proof ?? throw new Exception("Connection refused: no server proof.");

            if (!CryptographicOperations.FixedTimeEquals(clientProof, clientTranscriptRaw))
                throw new Exception("Connection refused: incorrect transcript HMAC.");

            return serverProof;
        }

        public async UnaryResult<byte[]> InvokeAsync(byte[] serializedEnv)
        {
            var env = MessagePack.MessagePackSerializer.Deserialize<EncryptedEnvelope>(serializedEnv);

            if (!_sessions.TryGet(env.ClientId, out SessionState s))
                throw SessionLost();

            var payload = s.FromClient.DecryptAndUnpack<Dictionary<string, object?>>(env);
            var request = new DiracRequest(env.Method, payload);

            // env.ClientId is the caller's authenticated identity. Route via the custom router
            // (Manager broker) if set, else dispatch locally through the worker's Ocean + gate.
            DiracResponse response = SecureWorkerHost.Router != null
                ? await SecureWorkerHost.Router(request, s.ToClient, env.ClientId)
                : await SecureWorkerHost.Ocean!.HandleRequestAsync(
                    request, s.ToClient, env.ClientId, SecureWorkerHost.Gate);

            return MessagePack.MessagePackSerializer.Serialize(response);
        }

        public async UnaryResult<bool> FinishAsync(byte[] serializedEnv)
        {
            var env = MessagePack.MessagePackSerializer.Deserialize<EncryptedEnvelope>(serializedEnv);

            if (!_sessions.TryGet(env.ClientId, out SessionState s))
                throw SessionLost();

            // Decrypting with the c2s key authenticates the teardown request.
            s.FromClient.Decrypt(env);
            _sessions.Remove(env.ClientId);
            SecureWorkerHost.ForgetClient(env.ClientId);
            return true;
        }
    }
}
