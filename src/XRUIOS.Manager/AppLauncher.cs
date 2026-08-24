using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace XRUIOS.Manager
{
    /// <summary>
    /// Lets apps be their own standalone exes. An app self-launches and ENROLLS with the running
    /// Manager over a local control pipe; the Manager ATTESTS the calling process — it must be running
    /// the registered app's binary from its registered install path, and that folder must be
    /// Notary-clean — before handing back credentials. No secret is stored at rest, and the app doesn't
    /// need the Manager to spawn it.
    ///
    /// Also supports "spawn &lt;app&gt;" so a shortcut can ask the Manager to start a registered app.
    ///
    /// Attestation properties:
    ///   • Identity comes from the PEER PROCESS (peer PID → its executable path), never from the claim.
    ///   • The peer must be running the app from its REGISTERED path, AND that folder must pass the
    ///     Notary check — so a copied-elsewhere binary (path mismatch) or a swapped sibling DLL
    ///     (Notary mismatch) is refused. Creds go only to the attested peer over a local pipe.
    ///
    /// Cross-platform: the transport is a .NET named pipe (a Unix domain socket under /tmp on Linux).
    /// Peer PID comes from GetNamedPipeClientProcessId on Windows and SO_PEERCRED on Linux.
    /// </summary>
    public sealed class AppLauncher
    {
        public const string PipeName = "xruios-manager-control";

        private sealed record Entry(string ExePath, AppRegistry.AppCredentials Cred);

        private readonly NotaryVerifier _notary;
        private readonly string _brokerAddress;
        private readonly ConcurrentDictionary<string, Entry> _catalog = new(StringComparer.Ordinal);

        public AppLauncher(NotaryVerifier notary, string brokerAddress)
        {
            _notary = notary;
            _brokerAddress = brokerAddress;
        }

        /// <summary>Register a launchable/enrollable app: its exe + the creds the Manager minted for it.</summary>
        public void Register(string appName, string exePath, AppRegistry.AppCredentials cred)
            => _catalog[appName] = new Entry(Path.GetFullPath(exePath), cred);

        public IEnumerable<string> Apps => _catalog.Keys;

        /// <summary>Securely spawn a registered app (checksum + env-handoff). Used by "spawn".</summary>
        public async Task<bool> LaunchAsync(string appName)
        {
            if (!_catalog.TryGetValue(appName, out var e)) return false;
            if (!await ChecksumOk(appName, e)) return false;

            var psi = new ProcessStartInfo { FileName = e.ExePath, UseShellExecute = false };
            psi.Environment["XRUIOS_APP_ID"] = e.Cred.AppId;
            psi.Environment["XRUIOS_APP_PSK"] = Convert.ToBase64String(e.Cred.Psk);
            psi.Environment["XRUIOS_BROKER_ADDR"] = _brokerAddress;
            Process.Start(psi);
            return true;
        }

        /// <summary>Background loop: handle one control request at a time ("spawn" or attested "enroll").</summary>
        public void StartControlServer(CancellationToken token)
        {
            _ = Task.Run(async () =>
            {
                var buf = new byte[1024];
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream(
                            PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                        await server.WaitForConnectionAsync(token);

                        int n = await server.ReadAsync(buf, token);
                        string request = Encoding.UTF8.GetString(buf, 0, n).Trim();
                        string reply = await HandleAsync(request, server);

                        await server.WriteAsync(Encoding.UTF8.GetBytes(reply), token);
                        await server.FlushAsync(token);
                        server.WaitForPipeDrain();
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex) { Console.Error.WriteLine($"[Launcher] control error: {ex.Message}"); }
                }
            }, token);
        }

        private async Task<string> HandleAsync(string request, NamedPipeServerStream server)
        {
            if (request.StartsWith("spawn ", StringComparison.Ordinal))
            {
                string name = request[6..].Trim();
                bool ok = await LaunchAsync(name);
                Console.WriteLine($"[Launcher] spawn '{name}' -> {(ok ? "launched" : "unknown/failed")}");
                return ok ? $"launched {name}" : $"no such app or checksum failed: {name}";
            }

            if (request.StartsWith("enroll ", StringComparison.Ordinal))
                return await AttestAndEnrollAsync(request[7..].Trim(), server);

            return "bad request (expected: spawn <app> | enroll <app>)";
        }

        // Verify the CALLER is the registered app running from its registered path, then return creds.
        // "OK\t<appId>\t<pskBase64>\t<broker>" on success, "DENY\t<reason>" otherwise.
        private async Task<string> AttestAndEnrollAsync(string appName, NamedPipeServerStream server)
        {
            if (!_catalog.TryGetValue(appName, out var e))
                return "DENY\tno such registered app";

            if (!TryGetPeerPid(server, out uint pid))
                return "DENY\tcould not identify the calling process";

            string? peerExe = GetProcessExePath(pid);
            if (peerExe == null)
                return "DENY\tcould not read the caller's binary";

            // Identity + location: the caller must be running the app FROM its registered install path.
            if (!PathEquals(Path.GetFullPath(peerExe), e.ExePath))
                return "DENY\tcaller is not the registered app (path mismatch)";

            // Integrity: that install folder must be untampered (this covers the managed DLL, not just
            // the native apphost).
            if (!await ChecksumOk(appName, e))
                return "DENY\tapp failed integrity check";

            Console.WriteLine($"[Launcher] ATTESTED + enrolled '{appName}' (pid {pid}) — {peerExe}");
            return $"OK\t{e.Cred.AppId}\t{Convert.ToBase64String(e.Cred.Psk)}\t{_brokerAddress}";
        }

        private async Task<bool> ChecksumOk(string appName, Entry e)
        {
            var desc = new WorkerDescriptor { Name = "app:" + appName, ExecutablePath = e.ExePath };
            if (await _notary.IsCleanAsync(desc)) return true;
            Console.Error.WriteLine($"[Launcher] '{appName}' failed checksum.");
            return false;
        }

        /// <summary>Client side: ask a running Manager to spawn <paramref name="appName"/>.</summary>
        public static async Task<string> RequestSpawnAsync(string appName)
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            try { await client.ConnectAsync(3000); }
            catch (TimeoutException) { return "No XRUIOS.Manager is running (control pipe not found)."; }

            await client.WriteAsync(Encoding.UTF8.GetBytes("spawn " + appName));
            await client.FlushAsync();
            var buf = new byte[1024];
            int n = await client.ReadAsync(buf);
            return n > 0 ? Encoding.UTF8.GetString(buf, 0, n) : "(no response)";
        }

        // ---- cross-platform peer identity ----

        private static bool PathEquals(string a, string b) =>
            string.Equals(a, b, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        private static string? GetProcessExePath(uint pid)
        {
            // Works on both platforms; on Linux it resolves /proc/<pid>/exe.
            try { return Process.GetProcessById((int)pid).MainModule?.FileName; }
            catch { return null; }
        }

        private static bool TryGetPeerPid(NamedPipeServerStream server, out uint pid)
        {
            pid = 0;
            try
            {
                if (OperatingSystem.IsWindows())
                    return GetNamedPipeClientProcessId(server.SafePipeHandle, out pid);

                if (OperatingSystem.IsLinux())
                    return TryGetPeerPidLinux(server.SafePipeHandle, out pid);

                return false; // other platforms: no attestation source
            }
            catch { return false; }
        }

        // Windows: the pipe knows its client's PID directly.
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetNamedPipeClientProcessId(SafePipeHandle Pipe, out uint ClientProcessId);

        // Linux: the .NET named pipe is a Unix domain socket, so SO_PEERCRED gives the peer's ucred.
        private const int SOL_SOCKET = 1;
        private const int SO_PEERCRED = 17; // x86_64 / arm64

        [DllImport("libc", SetLastError = true, EntryPoint = "getsockopt")]
        private static extern int getsockopt(int sockfd, int level, int optname, byte[] optval, ref int optlen);

        private static bool TryGetPeerPidLinux(SafePipeHandle handle, out uint pid)
        {
            pid = 0;
            int fd = (int)handle.DangerousGetHandle();
            // struct ucred { pid_t pid; uid_t uid; gid_t gid; } — three 32-bit ints.
            byte[] ucred = new byte[12];
            int len = ucred.Length;
            if (getsockopt(fd, SOL_SOCKET, SO_PEERCRED, ucred, ref len) != 0)
                return false;
            pid = BitConverter.ToUInt32(ucred, 0);
            return pid != 0;
        }
    }
}
