using System.Diagnostics;
using System.Security.Cryptography;
using MessagePack;
using Microsoft.AspNetCore.Builder;
using XRUIOS.Interfaces;
using static Pariah_Cybersecurity.EasyPQC;

namespace XRUIOS.Manager
{
    // The orchestration behind the CLI verbs. Keeps the copied helpers (WorkerSupervisor, BrokerRouter,
    // PermissionService, AppRegistry, NotaryVerifier) and adds the singleton guard, the login/key
    // authority, and the run loop.
    internal static class ManagerHost
    {
        private const string StopEventName = @"Global\XRUIOS.Manager.Stop";

        // Canonical roots under the user's local app data, so a test run and a real run stay separate
        // and the paths don't depend on the working directory.
        internal static string RootDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XRUIOS");
        internal static string DataPath => Path.Combine(RootDir, "Data");
        internal static string PublicDataPath => Path.Combine(RootDir, "Public");
        private static string PermDir => Path.Combine(RootDir, "Permissions");
        private static string BaselineDir => Path.Combine(RootDir, "Baselines");

        // ---- run ----
        // `external` lets a service host stop the Manager via its own cancellation (see ServiceHost).
        public static async Task<int> RunAsync(string[] args, CancellationToken external = default)
        {
            using var singleton = new SingletonGuard();
            if (!singleton.IsPrimary)
            {
                Console.Error.WriteLine("[Manager] Another XRUIOS.Manager is already running on this device.");
                return 66;
            }

            // On awake, harden the trusted core: block extension-point injection and watch for a debugger.
            SecureWorkerHost.HardenProcess(() =>
            {
                Console.Error.WriteLine("[Manager] Debugger detected - shutting down.");
                Environment.Exit(SecureWorkerHost.TamperExit);
            });

            Directory.CreateDirectory(DataPath);
            Directory.CreateDirectory(PublicDataPath);

            Console.WriteLine("=== XRUIOS.Manager ===");

            var keys = new KeyAuthority();
            var login = new LoginService(PublicDataPath);

            // Seamless login: unseal the primary user's master key from this OS session, if it was sealed
            // by a prior `login`. Multi-user selection is a UI concern; here the first user is primary.
            var primary = login.ListUsers().FirstOrDefault();
            if (primary.Uuid is not null)
            {
                byte[]? master = login.TrySealedLogin(primary.Uuid);
                if (master is not null)
                {
                    keys.Unlock(master);
                    CryptographicOperations.ZeroMemory(master);
                    Console.WriteLine($"[Manager] Session unlocked for {primary.Username} ({primary.Uuid}).");
                }
            }

            WorkerSupervisor? supervisor = null;
            BrokerRouter? router = null;
            WebApplication? broker = null;

            if (keys.IsUnlocked)
            {
                (broker, supervisor, router) = await StartCoreAsync(keys);
                await LaunchWorkersAsync(supervisor);
            }
            else
            {
                Console.WriteLine("[Manager] Locked. Run `XRUIOS.Manager login`, then start again.");
            }

            Console.WriteLine("[Manager] Up. Ctrl+C to stop.");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(external);
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
            _ = Task.Run(() =>
            {
                using var stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopEventName);
                stop.WaitOne();
                cts.Cancel();
            });

            try { await Task.Delay(Timeout.Infinite, cts.Token); }
            catch (TaskCanceledException) { }

            Console.WriteLine("[Manager] Shutting down...");
            if (router is not null) await router.DisposeAsync();
            supervisor?.StopAll();
            if (broker is not null) await broker.StopAsync();
            return 0;
        }

        // Stand up the trusted core once unlocked: permission store, Manager identity keys, the worker
        // registry, and the permission-checked broker.
        private static async Task<(WebApplication broker, WorkerSupervisor supervisor, BrokerRouter router)>
            StartCoreAsync(KeyAuthority keys)
        {
            var permissions = new PermissionService(PermDir, keys.PermissionStoreKey());
            await permissions.EnsureStoreAsync();

            // Register apps + their grants. The sample app is granted the two shareable Calendar
            // capabilities but NOT DeleteEvent, so its last call is refused (see the demo).
            var apps = new AppRegistry();
            var sampleCred = apps.Register("sampleapp");
            await permissions.GrantAsync(sampleCred.AppId, "GetEvents");
            await permissions.GrantAsync(sampleCred.AppId, "AddEvent");
            Console.WriteLine($"[Manager] Granted {sampleCred.AppId}: [GetEvents, AddEvent] (DeleteEvent withheld).");

            // The Diagnostics harness (the old Program.cs test suite, reborn as a fleet self-test). It's
            // granted every worker's health probe (<Short>.Ping) plus Calendar's GetEvents/AddEvent so it
            // can prove a real round-trip. DeleteEvent is withheld on purpose - the harness verifies that
            // an ungranted call is refused, so a withheld capability is part of the test.
            var diagCred = apps.Register("diagnostics");
            foreach (var shortName in WorkerCatalog.AllShortNames)
                await permissions.GrantAsync(diagCred.AppId, shortName + ".Ping");
            await permissions.GrantAsync(diagCred.AppId, "GetEvents");
            await permissions.GrantAsync(diagCred.AppId, "AddEvent");
            Console.WriteLine($"[Manager] Granted {diagCred.AppId}: [all <Worker>.Ping, GetEvents, AddEvent].");

            var (managerPub, managerPriv) = await Signatures.CreateKeys();
            string managerPubB64 = Convert.ToBase64String(MessagePackSerializer.Serialize(managerPub));

            var notary = new NotaryVerifier(BaselineDir);
            var supervisor = new WorkerSupervisor(notary, managerPubB64, DataPath, PublicDataPath, keys.DeriveWorkerKey);
            var router = new BrokerRouter(supervisor, permissions, managerPriv);

            var broker = await SecureWorkerHost.RunBrokerAsync(
                "XRUIOS.Manager.Broker", apps.ResolvePsk, router.RouteAsync);

            string brokerAddr = Utils.SecureStore.Get<string>("XRUIOS.Manager.Broker") ?? "(unknown)";
            WriteDevCreds(sampleCred, brokerAddr, "sampleapp.creds");
            WriteDevCreds(diagCred, brokerAddr, "diagnostics.creds");
            Console.WriteLine($"[Manager] Broker at {brokerAddr}. Master key held; per-worker keys derived on launch.");
            return (broker, supervisor, router);
        }

        // Launch the per-class workers. Each is provisioned with its OWN key + PSK + paths by the
        // supervisor (the key derived from the master, the PSK freshly minted), then checksummed and
        // started. A worker only ever receives its own key.
        private static async Task LaunchWorkersAsync(WorkerSupervisor supervisor)
        {
            var catalog = WorkerCatalog.Discover();
            if (catalog.Count == 0)
            {
                Console.WriteLine("[Manager] No worker executables found. Build the workers, or set XRUIOS_WORKERS_ROOT.");
                return;
            }

            Console.WriteLine($"[Manager] Launching {catalog.Count} workers in parallel...");
            bool[] results = await Task.WhenAll(catalog.Select(e =>
                supervisor.StartAsync(new WorkerDescriptor { Name = e.Name, ExecutablePath = e.ExePath })));
            int up = results.Count(r => r);
            Console.WriteLine($"[Manager] {up}/{catalog.Count} workers up.");
        }

        // Dev-only bridge so the sample app can connect without the full enrollment endpoint: write
        // its creds where the app looks for them. A real app uses env handoff or attested enrollment.
        private static void WriteDevCreds(AppRegistry.AppCredentials cred, string brokerAddr, string fileName)
        {
            try
            {
                Directory.CreateDirectory(PublicDataPath);
                File.WriteAllText(Path.Combine(PublicDataPath, fileName),
                    $"AppId={cred.AppId}\nPsk={Convert.ToBase64String(cred.Psk)}\nBroker={brokerAddr}\n");
            }
            catch { /* dev convenience only */ }
        }

        // ---- login ----
        public static async Task<int> LoginAsync(string[] args)
        {
            var login = new LoginService(PublicDataPath);
            var existing = login.ListUsers().ToList();

            // Password may be passed as the 2nd arg for non-interactive setup/testing; otherwise prompt.
            if (existing.Count == 0)
            {
                Console.WriteLine("No XRUIOS users yet - creating the first account.");
                string username = args.Length > 0 ? args[0] : Prompt("Username: ");
                string pw = args.Length > 1 ? args[1] : PromptSecret("New XRUIOS password: ");
                if (args.Length <= 1)
                {
                    string pw2 = PromptSecret("Confirm password: ");
                    if (pw != pw2) { Console.Error.WriteLine("Passwords didn't match."); return 1; }
                }

                string uuid = Guid.NewGuid().ToString("N");
                byte[] master = login.CreateUser(uuid, username, pw, profileImagePath: null);
                CryptographicOperations.ZeroMemory(master);
                Console.WriteLine($"Created {username} ({uuid}) and sealed this session. Start the Manager to bring workers up.");
                return 0;
            }

            var user = existing[0]; // single-user for now; a login screen would choose
            string password = args.Length > 1 ? args[1] : PromptSecret($"XRUIOS password for {user.Username}: ");
            byte[]? key = login.LoginWithPassword(user.Uuid, password);
            if (key is null) { Console.Error.WriteLine("Login failed."); return 1; }
            CryptographicOperations.ZeroMemory(key);
            Console.WriteLine("Unlocked and sealed this session. Start (or restart) the Manager to provision workers.");
            await Task.CompletedTask;
            return 0;
        }

        // ---- start (detached) ----
        public static int StartDetached(string[] args)
        {
            var exe = Environment.ProcessPath ?? "XRUIOS.Manager";
            var psi = new ProcessStartInfo(exe, "run")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            Process.Start(psi);
            Console.WriteLine("[Manager] Started in the background.");
            return 0;
        }

        // ---- status ----
        public static int Status()
        {
            bool running = Mutex.TryOpenExisting(@"Global\XRUIOS.Manager.Singleton", out var m);
            m?.Dispose();
            Console.WriteLine(running ? "XRUIOS.Manager: RUNNING" : "XRUIOS.Manager: stopped");
            return running ? 0 : 1;
        }

        // ---- stop ----
        public static int Stop()
        {
            if (!EventWaitHandle.TryOpenExisting(StopEventName, out var stop))
            {
                Console.WriteLine("XRUIOS.Manager is not running.");
                return 1;
            }
            stop.Set();
            stop.Dispose();
            Console.WriteLine("Stop signal sent.");
            return 0;
        }

        private static string Prompt(string label)
        {
            Console.Write(label);
            return Console.ReadLine() ?? "";
        }

        private static string PromptSecret(string label)
        {
            Console.Write(label);
            var sb = new System.Text.StringBuilder();
            ConsoleKeyInfo k;
            while ((k = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
            {
                if (k.Key == ConsoleKey.Backspace) { if (sb.Length > 0) sb.Length--; }
                else if (!char.IsControl(k.KeyChar)) sb.Append(k.KeyChar);
            }
            Console.WriteLine();
            return sb.ToString();
        }
    }
}
