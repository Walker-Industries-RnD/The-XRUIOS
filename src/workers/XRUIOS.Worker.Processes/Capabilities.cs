using System.Diagnostics;
using System.Text.RegularExpressions;
using EclipseProject;
using XRUIOS.Barebones;
using XRUIOS.Interpreter;

namespace XRUIOS.Worker.Processes
{
    // What is running on this machine, snapshots of it, and (privileged) stopping a process.
    public static class ProcessesCapabilities
    {
        // Snapshot names become file names, so they are limited to plain characters.
        private static readonly Regex SafeName = new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.Compiled);

        [SeaOfDirac("Processes.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Processes: " + input;

        /// <summary>
        /// "pid|name|memoryMB|cpuPercent|windowTitle;..." biggest memory first. CPU needs a half-second
        /// sample, so it is only measured when <paramref name="includeCpu"/> is true.
        /// </summary>
        [SeaOfDirac("ListProcesses", new[] { "includeCpu", "limit" }, typeof(string), typeof(bool), typeof(long))]
        public static string List(bool includeCpu = false, long limit = 200)
        {
            var rows = ProcessesClass.GetCurrentProcesses(includeCpu)
                .OrderByDescending(p => p.MemoryMB)
                .Take((int)Math.Clamp(limit, 1, 2000))
                .Select(p => $"{p.ProcessId}|{Wire.Field(p.ProcessName)}|{p.MemoryMB}|{p.CpuPercent:0.0}|{Wire.Field(p.WindowTitle)}");
            return string.Join(';', rows);
        }

        /// <summary>Save the current process list under a name (empty = a timestamp). Returns the name used.</summary>
        [SeaOfDirac("SaveProcessSnapshot", new[] { "name" }, typeof(string), typeof(string))]
        public static async Task<string> Save(string name = "")
        {
            if (!string.IsNullOrEmpty(name)) CheckName(name);
            string path = await ProcessesClass.SaveProcessSnapshot(string.IsNullOrEmpty(name) ? null : name);
            return Path.GetFileNameWithoutExtension(path);
        }

        /// <summary>Saved snapshot names, newest first.</summary>
        [SeaOfDirac("ListProcessSnapshots", new string[0], typeof(string))]
        public static string Snapshots() => string.Join(';', ProcessesClass.GetSavedSnapshots());

        /// <summary>One saved snapshot, in the same row format as ListProcesses.</summary>
        [SeaOfDirac("GetProcessSnapshot", new[] { "name" }, typeof(string), typeof(string))]
        public static async Task<string> GetSnapshot(string name)
        {
            CheckName(name);
            var snapshot = await ProcessesClass.LoadProcessSnapshot(name)
                ?? throw new KeyNotFoundException($"No snapshot '{name}'.");
            return string.Join(';', snapshot.Processes.OrderByDescending(p => p.MemoryMB)
                .Select(p => $"{p.ProcessId}|{Wire.Field(p.ProcessName)}|{p.MemoryMB}|{p.CpuPercent:0.0}|{Wire.Field(p.WindowTitle)}"));
        }

        /// <summary>
        /// Stop a process. The name must match the process that has that id right now, so a stale id that
        /// has since been reused by another program is refused. XRUIOS's own processes are never stopped.
        /// </summary>
        [SeaOfDirac("KillProcess", new[] { "processId", "processName" }, typeof(string), typeof(long), typeof(string))]
        public static async Task<string> Kill(long processId, string processName)
        {
            if (processId <= 4 || processId > int.MaxValue) throw new ArgumentException("That is not a process id that can be stopped.");

            Process process;
            try { process = Process.GetProcessById((int)processId); }
            catch (ArgumentException) { throw new KeyNotFoundException($"No process with id {processId}."); }

            if (!string.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Process {processId} is '{process.ProcessName}', not '{processName}'.");
            if (process.Id == Environment.ProcessId || process.ProcessName.StartsWith("XRUIOS.", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("XRUIOS's own processes cannot be stopped from here.");

            if (!await ProcessesClass.KillProcess((int)processId, processName))
                throw new InvalidOperationException($"Process {processId} could not be stopped.");
            return "stopped";
        }

        private static void CheckName(string name)
        {
            if (!SafeName.IsMatch(name) || name.Contains("..", StringComparison.Ordinal))
                throw new ArgumentException("A snapshot name may use letters, digits, '.', '_' and '-' (up to 64 characters).");
        }
    }
}
