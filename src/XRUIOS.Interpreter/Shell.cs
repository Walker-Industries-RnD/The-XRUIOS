using System.Diagnostics;

namespace XRUIOS.Interpreter
{
    /// <summary>
    /// Runs a helper program with an explicit argument list - never through a shell - so text that
    /// came from an app (a notification body, a layout id) can't be interpreted as a command.
    /// </summary>
    internal static class Shell
    {
        internal sealed record Result(int ExitCode, string StdOut, string StdErr)
        {
            public bool Ok => ExitCode == 0;
        }

        private static readonly Dictionary<string, string?> WhichCache = new(StringComparer.Ordinal);

        /// <summary>Full path of <paramref name="command"/> on PATH, or null.</summary>
        public static string? Which(string command)
        {
            lock (WhichCache)
            {
                if (WhichCache.TryGetValue(command, out var cached)) return cached;

                string? found = null;
                var exts = OperatingSystem.IsWindows() ? new[] { ".exe", ".cmd", ".bat", "" } : new[] { "" };
                foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    foreach (var ext in exts)
                    {
                        var candidate = Path.Combine(dir, command + ext);
                        if (File.Exists(candidate)) { found = candidate; break; }
                    }
                    if (found != null) break;
                }
                return WhichCache[command] = found;
            }
        }

        public static bool Has(string command) => Which(command) != null;

        /// <param name="cLocale">Force LC_ALL=C so tools whose output we parse don't localize it.</param>
        public static Result Run(string file, IEnumerable<string> args, string? stdin = null, int timeoutMs = 5000, bool cLocale = false)
        {
            var psi = new ProcessStartInfo(file)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = stdin != null,
                CreateNoWindow = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            if (cLocale)
            {
                psi.Environment["LC_ALL"] = "C";
                psi.Environment["LANG"] = "C";
            }

            using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start '{file}'.");
            var stdoutTask = p.StandardOutput.ReadToEndAsync();
            var stderrTask = p.StandardError.ReadToEndAsync();
            if (stdin != null)
            {
                p.StandardInput.Write(stdin);
                p.StandardInput.Close();
            }

            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* already gone */ }
                throw new TimeoutException($"'{Path.GetFileName(file)}' did not finish within {timeoutMs} ms.");
            }
            p.WaitForExit();
            return new Result(p.ExitCode, stdoutTask.Result, stderrTask.Result);
        }

        /// <summary>Run and throw with the tool's own error text on a non-zero exit.</summary>
        public static string RunChecked(string file, IEnumerable<string> args, string? stdin = null, int timeoutMs = 5000, bool cLocale = false)
        {
            var args0 = args.ToList();
            var r = Run(file, args0, stdin, timeoutMs, cLocale);
            if (!r.Ok)
                throw new InvalidOperationException(
                    $"'{Path.GetFileName(file)} {string.Join(' ', args0)}' failed ({r.ExitCode}): {FirstLine(r.StdErr) ?? FirstLine(r.StdOut) ?? "no output"}");
            return r.StdOut;
        }

        /// <summary>
        /// Fire-and-forget for tools that fork a daemon and keep the pipes open (xclip, wl-copy): no
        /// stdout/stderr redirection, so we never block on a child that outlives the call.
        /// </summary>
        public static void RunDetached(string file, IEnumerable<string> args, string? stdin = null, int timeoutMs = 5000)
        {
            var psi = new ProcessStartInfo(file)
            {
                UseShellExecute = false,
                RedirectStandardInput = stdin != null,
                CreateNoWindow = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start '{file}'.");
            if (stdin != null)
            {
                p.StandardInput.Write(stdin);
                p.StandardInput.Close();
            }
            p.WaitForExit(timeoutMs);
        }

        public static string? FirstLine(string s)
        {
            var line = s.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
            return line;
        }

        internal static int Clamp01to100(int v) => Math.Clamp(v, 0, 100);
    }
}
