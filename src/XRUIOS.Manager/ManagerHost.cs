using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using XRUIOS.Interfaces;
using XRUIOS.Interpreter;

namespace XRUIOS.Manager
{
    // The orchestration behind the CLI verbs: the singleton guard, the login/key authority, the trusted
    // core (permissions, signing identity, app registry, broker, worker supervisor), and the run loop.
    internal static class ManagerHost
    {
        private const string StopEventName = @"Global\XRUIOS.Manager.Stop";

        /// <summary>`run --dev-creds` writes plaintext .creds files for apps launched outside the Manager.</summary>
        public const string DevCredsFlag = "--dev-creds";

        // Canonical roots under the user's local app data, so the paths don't depend on the working
        // directory. XRUIOS_ROOT moves everything elsewhere (the end-to-end tests use a throwaway root so a
        // test run never touches the real data).
        internal static string RootDir =>
            Environment.GetEnvironmentVariable("XRUIOS_ROOT")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XRUIOS");
        internal static string DataPath => Path.Combine(RootDir, "Data");
        internal static string PublicDataPath => Path.Combine(RootDir, "Public");
        private static string BaselineDir => Path.Combine(RootDir, "Baselines");

        // Everything below a user's folder is encrypted with THAT user's master key, so another user
        // opening the Manager fails the MAC instead of reading it.
        private static string UserDir(string uuid) => Path.Combine(RootDir, "Users", uuid);
        private static string PermDirFor(string uuid) => Path.Combine(UserDir(uuid), "Permissions");
        private static string IdentityPath(string uuid) => Path.Combine(UserDir(uuid), "manager-identity.bin");
        private static string AppStorePath(string uuid) => Path.Combine(UserDir(uuid), "apps.bin");
        private static string ManagerAuditPath(string uuid) => Path.Combine(UserDir(uuid), "Audit", "manager.log");
        private static string CertificatesDir(string uuid) => Path.Combine(UserDir(uuid), "Certificates");

        private static CertificateVault OpenVault(Session s, ManagerIdentity identity) =>
            new(CertificatesDir(s.Uuid), s.Keys.CertificateVaultKey(), identity.Public, identity.Private);

        // ---- unlocking (shared by every verb that needs the master key) ----

        private sealed record Session(KeyAuthority Keys, string Uuid, string Username);

        // The sealed OS-session copy first; with allowPrompt, fall back to asking for the password.
        private static Session? Unlock(bool allowPrompt)
        {
            var login = new LoginService(PublicDataPath);
            var primary = login.ListUsers().FirstOrDefault(); // single-user for now; a login screen would choose
            if (primary.Uuid is null)
            {
                Console.Error.WriteLine("No XRUIOS account yet. Run `XRUIOS.Manager login` first.");
                return null;
            }

            byte[]? master = login.TrySealedLogin(primary.Uuid);
            if (master is null && allowPrompt)
                master = login.LoginWithPassword(primary.Uuid, PromptSecret($"XRUIOS password for {primary.Username}: "));
            if (master is null) return null;

            var keys = new KeyAuthority();
            keys.Unlock(master);
            CryptographicOperations.ZeroMemory(master);
            return new Session(keys, primary.Uuid, primary.Username);
        }

        private static async Task<(ManagerIdentity identity, NotaryVerifier notary)> TrustAsync(Session s)
        {
            var identity = await ManagerIdentity.LoadOrCreateAsync(IdentityPath(s.Uuid), s.Keys.IdentityKey());
            return (identity, new NotaryVerifier(BaselineDir, identity.Public, identity.Private));
        }

        private static AuditTrail ManagerAudit(Session s) =>
            new(ManagerAuditPath(s.Uuid), AuditTrail.KeyFrom(s.Keys.ManagerAuditKey()));

        // Built-in apps first; an installed app can't take a built-in's name.
        private static List<AppManifest> AllApps(InstalledAppStore.State state)
        {
            var apps = AppCatalog.BuiltIns().ToList();
            var taken = apps.Select(a => a.Name).ToHashSet(StringComparer.Ordinal);
            apps.AddRange(state.Apps.Where(a => !taken.Contains(a.Name)));
            return apps;
        }

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

            bool devCreds = args.Contains(DevCredsFlag) || Environment.GetEnvironmentVariable("XRUIOS_DEV_CREDS") == "1";
            Core? core = null;

            // Seamless login: unseal the primary user's master key from this OS session, if it was sealed
            // by a prior `login`.
            var session = Unlock(allowPrompt: false);
            if (session is not null)
            {
                Console.WriteLine($"[Manager] Session unlocked for {session.Username} ({session.Uuid}).");
                core = await StartCoreAsync(session, devCreds);
                await LaunchWorkersAsync(core.Supervisor, core.Notary);
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
            if (core is not null)
            {
                core.ControlPipe.Cancel();
                await core.Router.AnchorAuditAsync();
                await core.EventPump.DisposeAsync();
                await core.Router.DisposeAsync();
                core.Supervisor.StopAll();
                await core.Broker.StopAsync();
                if (!devCreds) DeleteDevCreds();
            }
            return 0;
        }

        private sealed record Core(WebApplication Broker, WorkerSupervisor Supervisor, BrokerRouter Router,
            WorkerEventPump EventPump, NotaryVerifier Notary, CancellationTokenSource ControlPipe);

        // Stand up the trusted core once unlocked: permission store, the Manager's signing identity, the
        // app registry + enrollment pipe, the worker registry, and the permission-checked broker.
        private static async Task<Core> StartCoreAsync(Session session, bool devCreds)
        {
            var keys = session.Keys;
            var permissions = new PermissionService(PermDirFor(session.Uuid), keys.PermissionStoreKey());
            await permissions.EnsureStoreAsync();

            var (identity, notary) = await TrustAsync(session);
            var audit = ManagerAudit(session);

            // Certificates: seal whatever was dropped in the incoming folder, then re-check everything sealed.
            // This runs before the broker and workers start, while nothing else is using the signing code.
            var vault = OpenVault(session, identity);
            await vault.InitializeAsync();
            foreach (var name in await vault.ScanAndSealAsync())
                audit.Append("manager", "cert-import", name, ok: true, "from the incoming folder");
            var certs = await vault.ListAsync();
            if (certs.Count > 0)
                Console.WriteLine($"[Manager] {certs.Count(c => c.Valid)}/{certs.Count} sealed certificates verify.");
            foreach (var bad in certs.Where(c => !c.Valid))
            {
                Console.Error.WriteLine($"[Manager] Certificate '{bad.Name}' no longer verifies: {bad.Problem}");
                audit.Append("manager", "cert-invalid", bad.Name, ok: false, bad.Problem);
            }

            // Every app gets a fresh PSK for this run and exactly the grants its manifest lists.
            var registry = new AppRegistry();
            var store = new InstalledAppStore(AppStorePath(session.Uuid), keys.AppRegistryKey());
            var state = store.Load();
            var apps = AllApps(state);
            var creds = new Dictionary<string, AppRegistry.AppCredentials>(StringComparer.Ordinal);
            foreach (var app in apps)
            {
                creds[app.Name] = registry.Register(app.Name);
                await SyncGrantsAsync(permissions, app, state);
            }
            store.Save(state);
            Console.WriteLine($"[Manager] {apps.Count} apps registered " +
                              $"({apps.Count(a => a.Exe != null)} with a known executable, enrollable).");

            var supervisor = new WorkerSupervisor(notary, identity.PublicBase64, DataPath, PublicDataPath, keys.DeriveWorkerKey);
            var events = new EventHub();
            var router = new BrokerRouter(supervisor, permissions, identity.Private, events, audit);
            var pump = new WorkerEventPump(supervisor, events, identity.Private);
            pump.Start();

            var broker = await SecureWorkerHost.RunBrokerAsync(
                "XRUIOS.Manager.Broker",
                // "<appId>#events" is the same app's event-stream session: same key.
                id => registry.ResolvePsk(XruiosEventProtocol.BaseIdentity(id)),
                router.RouteAsync);
            string brokerAddr = Utils.SecureStore.Get<string>("XRUIOS.Manager.Broker") ?? "(unknown)";

            // Apps get their credentials by enrolling over the control pipe: the Manager checks the
            // calling process is the registered exe in its approved folder, then answers.
            var launcher = new AppLauncher(notary, brokerAddr) { Audit = audit };
            foreach (var app in apps.Where(a => a.Exe != null))
                launcher.Register(app.Name, app.Exe!, creds[app.Name]);
            var controlPipe = new CancellationTokenSource();
            launcher.StartControlServer(controlPipe.Token);

            // Anchor the worker audit logs every few minutes (and once more at shutdown).
            _ = Task.Run(async () =>
            {
                while (!controlPipe.IsCancellationRequested)
                {
                    try { await Task.Delay(TimeSpan.FromMinutes(5), controlPipe.Token); }
                    catch (OperationCanceledException) { return; }
                    await router.AnchorAuditAsync();
                }
            });

            if (devCreds)
            {
                foreach (var (name, cred) in creds) WriteDevCreds(cred, brokerAddr, name + ".creds");
                Console.WriteLine($"[Manager] DEV: wrote plaintext .creds for {creds.Count} apps to {PublicDataPath}.");
            }
            else
            {
                DeleteDevCreds();
            }

            Console.WriteLine($"[Manager] Broker at {brokerAddr}. Enrollment pipe '{AppLauncher.PipeName}' open.");
            return new Core(broker, supervisor, router, pump, notary, controlPipe);
        }

        // Grant what the manifest lists; revoke what an earlier version granted and this one doesn't.
        private static async Task SyncGrantsAsync(PermissionService permissions, AppManifest app, InstalledAppStore.State state)
        {
            var want = app.Capabilities
                .Concat(app.EventList.Select(XruiosEventProtocol.PermissionFor))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var had = state.Granted.TryGetValue(app.Name, out var prev) ? prev
                : AppCatalog.LegacyGrants.GetValueOrDefault(app.Name, Array.Empty<string>());

            foreach (var gone in had.Except(want, StringComparer.Ordinal))
                await permissions.RevokeAsync(app.AppId, gone);
            foreach (var cap in want)
                await permissions.GrantAsync(app.AppId, cap);
            state.Granted[app.Name] = want;
        }

        // Launch the per-class workers, a few at a time. Starting all ~28 at once (each a full .NET +
        // Kestrel process, each hashing its own folder) spikes committed memory past what a 16 GB machine
        // has. XRUIOS_LAUNCH_PARALLEL tunes the batch size.
        private static async Task LaunchWorkersAsync(WorkerSupervisor supervisor, NotaryVerifier notary)
        {
            var catalog = WorkerCatalog.Discover();
            if (catalog.Count == 0)
            {
                Console.WriteLine("[Manager] No worker executables found. Build the workers, or set XRUIOS_WORKERS_ROOT.");
                return;
            }

            int parallel = int.TryParse(Environment.GetEnvironmentVariable("XRUIOS_LAUNCH_PARALLEL"), out int p) && p > 0 ? p : 3;
            Console.WriteLine($"[Manager] Launching {catalog.Count} workers, {parallel} at a time...");

            using var gate = new SemaphoreSlim(parallel);
            bool[] results = await Task.WhenAll(catalog.Select(async e =>
            {
                await gate.WaitAsync();
                try { return await supervisor.StartAsync(new WorkerDescriptor { Name = e.Name, ExecutablePath = e.ExePath }); }
                finally { gate.Release(); }
            }));

            int up = results.Count(r => r);
            Console.WriteLine($"[Manager] {up}/{catalog.Count} workers up.");
            if (up < catalog.Count)
                Console.WriteLine("[Manager] Workers that aren't approved stay down. After building or updating, run `XRUIOS.Manager baseline`.");
        }

        // ---- baseline ----

        /// <summary>
        /// `XRUIOS.Manager baseline [name...]`: approve the worker and app folders as they are now. Each
        /// folder is hashed and the tree is signed with the Manager's identity key; only signed trees are
        /// trusted at launch. Run it after installing or rebuilding. With names, only those are approved.
        /// </summary>
        public static async Task<int> ApproveAsync(string[] names)
        {
            var session = Unlock(allowPrompt: true);
            if (session is null) { Console.Error.WriteLine("Login failed."); return 1; }
            var (_, notary) = await TrustAsync(session);
            var audit = ManagerAudit(session);
            var state = new InstalledAppStore(AppStorePath(session.Uuid), session.Keys.AppRegistryKey()).Load();

            var targets = WorkerCatalog.Discover()
                .Select(w => new WorkerDescriptor { Name = w.Name, ExecutablePath = w.ExePath })
                .Concat(AllApps(state).Where(a => a.Exe != null).Select(AppCatalog.Descriptor))
                .Where(t => names.Length == 0 || names.Any(n => Matches(t.Name, n)))
                .ToList();

            if (targets.Count == 0)
            {
                Console.Error.WriteLine("Nothing to approve (no built workers/apps found, or no name matched).");
                return 1;
            }

            foreach (var t in targets)
            {
                int files = await notary.ApproveAsync(t);
                audit.Append(session.Username, "approve", t.Name, ok: true, $"{files} files at {t.Folder}");
                Console.WriteLine($"  approved {t.Name,-34} {files,5} files  {t.Folder}");
            }
            Console.WriteLine($"[Manager] Signed {targets.Count} baselines. Restart the Manager to launch with them.");
            return 0;
        }

        // "Calendar" matches XRUIOS.Worker.Calendar; "XRUIOS.App.Time" matches app:XRUIOS.App.Time.
        private static bool Matches(string target, string query) =>
            string.Equals(target, query, StringComparison.OrdinalIgnoreCase) ||
            target.EndsWith("." + query, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target, "app:" + query, StringComparison.OrdinalIgnoreCase);

        // ---- installed apps ----

        /// <summary>`XRUIOS.Manager install-app &lt;manifest.json&gt;`: register a third-party app and approve its folder.</summary>
        public static async Task<int> InstallAppAsync(string[] args)
        {
            if (args.Length < 1) { Console.Error.WriteLine("Usage: XRUIOS.Manager install-app <manifest.json>"); return 1; }

            AppManifest? m;
            try { m = JsonSerializer.Deserialize<AppManifest>(File.ReadAllText(args[0])); }
            catch (Exception ex) { Console.Error.WriteLine($"Can't read the manifest: {ex.Message}"); return 1; }

            string? problem = m is null ? "empty manifest"
                : string.IsNullOrWhiteSpace(m.Name) || !m.Name.All(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_') ? "name must be letters, digits, '.', '-' or '_'"
                : AppCatalog.BuiltIns().Any(b => b.Name == m.Name) ? $"'{m.Name}' is a built-in app"
                : string.IsNullOrEmpty(m.Exe) || !File.Exists(m.Exe) ? $"exe not found: {m.Exe}"
                : m.Capabilities is null ? "capabilities missing"
                : null;
            if (problem != null) { Console.Error.WriteLine($"Not installed: {problem}."); return 1; }

            var session = Unlock(allowPrompt: true);
            if (session is null) { Console.Error.WriteLine("Login failed."); return 1; }
            var (_, notary) = await TrustAsync(session);

            var app = m! with { Exe = Path.GetFullPath(m!.Exe!) };
            var store = new InstalledAppStore(AppStorePath(session.Uuid), session.Keys.AppRegistryKey());
            var state = store.Load();
            state.Apps.RemoveAll(a => a.Name == app.Name);
            state.Apps.Add(app);
            store.Save(state);

            int files = await notary.ApproveAsync(AppCatalog.Descriptor(app));
            ManagerAudit(session).Append(session.Username, "install-app", app.AppId, ok: true,
                $"{app.Exe}; caps=[{string.Join(",", app.Capabilities)}]; events=[{string.Join(",", app.EventList)}]");

            Console.WriteLine($"Installed {app.AppId} ({files} files approved) with {app.Capabilities.Length} capabilities " +
                              $"and {app.EventList.Length} events. Restart the Manager to apply the grants.");
            return 0;
        }

        /// <summary>`XRUIOS.Manager uninstall-app &lt;name&gt;`: remove it, revoke its grants and forget its approval.</summary>
        public static async Task<int> UninstallAppAsync(string[] args)
        {
            if (args.Length < 1) { Console.Error.WriteLine("Usage: XRUIOS.Manager uninstall-app <name>"); return 1; }
            string name = args[0].StartsWith("app:") ? args[0][4..] : args[0];

            var session = Unlock(allowPrompt: true);
            if (session is null) { Console.Error.WriteLine("Login failed."); return 1; }
            var store = new InstalledAppStore(AppStorePath(session.Uuid), session.Keys.AppRegistryKey());
            var state = store.Load();
            if (state.Apps.RemoveAll(a => a.Name == name) == 0)
            {
                Console.Error.WriteLine($"No installed app named '{name}' (built-in apps can't be uninstalled).");
                return 1;
            }

            var permissions = new PermissionService(PermDirFor(session.Uuid), session.Keys.PermissionStoreKey());
            await permissions.EnsureStoreAsync();
            foreach (var cap in state.Granted.GetValueOrDefault(name, Array.Empty<string>()))
                await permissions.RevokeAsync("app:" + name, cap);
            state.Granted.Remove(name);
            store.Save(state);

            var (_, notary) = await TrustAsync(session);
            notary.Revoke("app:" + name);
            ManagerAudit(session).Append(session.Username, "uninstall-app", "app:" + name, ok: true);
            Console.WriteLine($"Uninstalled app:{name}. Its grants are revoked; restart the Manager to drop its session.");
            return 0;
        }

        /// <summary>`XRUIOS.Manager apps`: every app the Manager will register, with its exe and grants.</summary>
        public static int ListApps()
        {
            var session = Unlock(allowPrompt: true);
            if (session is null) { Console.Error.WriteLine("Login failed."); return 1; }
            var state = new InstalledAppStore(AppStorePath(session.Uuid), session.Keys.AppRegistryKey()).Load();
            var builtIn = AppCatalog.BuiltIns().Select(a => a.Name).ToHashSet();

            foreach (var app in AllApps(state))
            {
                Console.WriteLine($"{app.AppId}  [{(builtIn.Contains(app.Name) ? "built-in" : "installed")}]");
                Console.WriteLine($"    exe:    {app.Exe ?? "(not built / not found)"}");
                Console.WriteLine($"    caps:   {string.Join(", ", app.Capabilities)}");
                if (app.EventList.Length > 0) Console.WriteLine($"    events: {string.Join(", ", app.EventList)}");
            }
            return 0;
        }

        // ---- certificates ----

        /// <summary>
        /// `XRUIOS.Manager cert add &lt;file&gt; | list | verify | remove &lt;name&gt;`: the certificate vault. A certificate
        /// is encrypted, signed with the Manager's identity and checked before it is trusted; `verify` re-checks all.
        /// </summary>
        public static async Task<int> CertAsync(string[] args)
        {
            const string usage = "Usage: XRUIOS.Manager cert add <file> | list | verify | remove <name>";
            if (args.Length == 0) { Console.Error.WriteLine(usage); return 1; }

            var session = Unlock(allowPrompt: true);
            if (session is null) { Console.Error.WriteLine("Login failed."); return 1; }
            var (identity, _) = await TrustAsync(session);
            var vault = OpenVault(session, identity);
            await vault.InitializeAsync();
            var audit = ManagerAudit(session);

            switch (args[0].ToLowerInvariant())
            {
                case "add" when args.Length >= 2:
                    try
                    {
                        string name = await vault.ImportAsync(args[1]);
                        audit.Append(session.Username, "cert-add", name, ok: true);
                        Console.WriteLine($"Sealed and verified '{name}'.");
                        return 0;
                    }
                    catch (Exception ex)
                    {
                        audit.Append(session.Username, "cert-add", args[1], ok: false, ex.Message);
                        Console.Error.WriteLine(ex.Message);
                        return 1;
                    }

                case "list":
                case "verify":
                    var entries = await vault.ListAsync();
                    foreach (var c in entries)
                        Console.WriteLine($"  {(c.Valid ? "OK     " : "INVALID")}  {c.Name,-40} {c.Bytes,8} bytes  {c.SealedAt.ToLocalTime():yyyy-MM-dd HH:mm}{(c.Problem is null ? "" : "  " + c.Problem)}");
                    if (entries.Count == 0) Console.WriteLine($"  No certificates. Drop files in {vault.IncomingDir} or use `cert add <file>`.");
                    return entries.Any(c => !c.Valid) ? 3 : 0;

                case "remove" when args.Length >= 2:
                    bool removed = vault.Remove(args[1]);
                    audit.Append(session.Username, "cert-remove", args[1], ok: removed);
                    Console.WriteLine(removed ? $"Removed '{args[1]}'." : $"No certificate named '{args[1]}'.");
                    return removed ? 0 : 1;

                default:
                    Console.Error.WriteLine(usage);
                    return 1;
            }
        }

        // ---- audit ----

        /// <summary>
        /// `XRUIOS.Manager audit [log] [count]`: verify the Manager's log and every worker's log, then print
        /// the last entries of one ("manager" by default, or a worker name such as Calendar).
        /// </summary>
        public static int Audit(string[] args)
        {
            var session = Unlock(allowPrompt: true);
            if (session is null) { Console.Error.WriteLine("Login failed."); return 1; }

            var logs = new List<(string name, string path, byte[] key)>
            {
                ("manager", ManagerAuditPath(session.Uuid), AuditTrail.KeyFrom(session.Keys.ManagerAuditKey())),
            };
            string workerDir = Path.Combine(DataPath, "Audit");
            if (Directory.Exists(workerDir))
                foreach (var f in Directory.EnumerateFiles(workerDir, "*.log").OrderBy(f => f))
                {
                    string worker = Path.GetFileNameWithoutExtension(f);
                    logs.Add((worker, f, AuditTrail.KeyFrom(session.Keys.DeriveWorkerKey(worker))));
                }

            // The newest anchor the Manager recorded for each worker log: that entry must still be there.
            var anchors = new Dictionary<string, (long seq, string mac)>(StringComparer.Ordinal);
            foreach (var e in AuditTrail.ReadEntries(logs[0].path))
                if (e.Action == "anchor" && e.Ok && e.Detail?.Split('|') is [var s, var m] && long.TryParse(s, out long seq))
                    anchors[e.Target] = (seq, m);

            int bad = 0;
            foreach (var (name, path, key) in logs)
            {
                var r = AuditTrail.Verify(path, key);
                string? problem = r.Ok ? null : $"at entry {r.FirstBadSeq}: {r.Problem}";
                if (problem is null && anchors.TryGetValue(name, out var anchor) &&
                    !AuditTrail.ReadEntries(path).Any(e => e.Seq == anchor.seq && e.Mac == anchor.mac))
                    problem = $"entry {anchor.seq} that the Manager anchored is gone (log cut short or replaced)";

                if (problem != null) bad++;
                Console.WriteLine(problem is null
                    ? $"  OK      {name,-34} {r.Entries,7} entries{(anchors.ContainsKey(name) ? ", anchored" : "")}"
                    : $"  BROKEN  {name,-34} {problem}");
            }

            string pick = args.Length > 0 ? args[0] : "manager";
            int count = args.Length > 1 && int.TryParse(args[1], out int n) ? n : 15;
            var chosen = logs.FirstOrDefault(l => Matches(l.name, pick));
            if (chosen.path != null)
            {
                Console.WriteLine($"\nLast {count} of {chosen.name}:");
                foreach (var e in AuditTrail.ReadEntries(chosen.path).TakeLast(count))
                    Console.WriteLine($"  #{e.Seq,-6} {e.At.ToLocalTime():yyyy-MM-dd HH:mm:ss}  {(e.Ok ? "ok  " : "FAIL")}  {e.Actor,-28} {e.Action,-12} {e.Target} {e.Detail}");
            }
            return bad == 0 ? 0 : 3;
        }

        // ---- dev creds ----

        // Dev-only bridge for apps run outside the Manager's view (e.g. from an IDE): their creds as a
        // plaintext file. Off unless `run --dev-creds`; otherwise any left over are removed, because a
        // readable PSK lets any local process act as that app until the Manager restarts.
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

        private static void DeleteDevCreds()
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(PublicDataPath, "*.creds")) File.Delete(f);
            }
            catch { /* best effort */ }
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
                Console.WriteLine($"Created {username} ({uuid}) and sealed this session.");
                Console.WriteLine("Next: `XRUIOS.Manager baseline` to approve the installed workers and apps, then start the Manager.");
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
            var psi = new ProcessStartInfo(exe, "run " + string.Join(" ", args))
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
            if (Console.IsInputRedirected) return Console.ReadLine() ?? "";
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
