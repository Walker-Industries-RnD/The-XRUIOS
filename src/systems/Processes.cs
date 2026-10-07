using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Pariah_Cybersecurity.DataHandler;
using static XRUIOS.Barebones.Interfaces.ProcessesClass;
using static XRUIOS.Barebones.XRUIOS;

namespace XRUIOS.Barebones
{
    public static class ProcessesClass
    {
        private const int MaxProcessHistory = 100;
        private static readonly TimeSpan CpuSampleInterval = TimeSpan.FromMilliseconds(500);

     
        public static List<ProcessInfo> GetCurrentProcesses(bool includeCpu = true)
        {
            var processes = new List<ProcessInfo>();
            var currentDevice = Environment.MachineName;

            // Batch CPU sampling for efficiency (cross-platform)
            Dictionary<int, TimeSpan>? startCpuTimes = null;
            if (includeCpu)
            {
                startCpuTimes = BatchGetStartCpuTimes();
                Thread.Sleep(CpuSampleInterval);
            }

            foreach (var proc in Process.GetProcesses())
            {
                if (proc.Id == 0) continue;

                try
                {
                    long memoryMB = proc.WorkingSet64 / 1024 / 1024;
                    float cpuPercent = includeCpu ? CalculateCpuPercent(proc, startCpuTimes) : 0f;

                    processes.Add(new ProcessInfo(
                        DeviceId: currentDevice,
                        ProcessId: proc.Id,
                        ProcessName: proc.ProcessName,
                        ProcessType: "System", // TODO: improve detection (e.g., check paths for Steam/Game/App)
                        WindowTitle: GetMainWindowTitle(proc),
                        StartTime: GetProcessStartTime(proc),
                        MemoryMB: memoryMB,
                        CpuPercent: cpuPercent,
                        ExecutablePath: GetExecutablePath(proc),
                        IsResponsive: proc.Responding
                    ));
                }
                catch { /* Access denied or zombie process */ }
            }

            return processes.OrderBy(p => p.ProcessName).ToList();
        }

        private static Dictionary<int, TimeSpan> BatchGetStartCpuTimes()
        {
            var startTimes = new Dictionary<int, TimeSpan>();
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (proc.Id != 0)
                        startTimes[proc.Id] = proc.TotalProcessorTime;
                }
                catch { /* Skip inaccessible */ }
            }
            return startTimes;
        }

        private static float CalculateCpuPercent(Process proc, Dictionary<int, TimeSpan>? startCpuTimes)
        {
            if (startCpuTimes == null || !startCpuTimes.TryGetValue(proc.Id, out var startCpu))
                return 0f;

            try
            {
                var endCpu = proc.TotalProcessorTime;
                var cpuDeltaMs = (endCpu - startCpu).TotalMilliseconds;
                var timeDeltaMs = CpuSampleInterval.TotalMilliseconds;
                var numCores = Environment.ProcessorCount;
                var usage = (cpuDeltaMs / (timeDeltaMs * numCores)) * 100;
                return (float)Math.Min(usage, 100.0); // Clamp to 100%
            }
            catch
            {
                return 0f;
            }
        }

        private static string? GetMainWindowTitle(Process p) => p.Safe(() => string.IsNullOrEmpty(p.MainWindowTitle) ? null : p.MainWindowTitle);
        private static DateTime GetProcessStartTime(Process p) => p.Safe(() => p.StartTime.ToUniversalTime(), DateTime.UtcNow);
        private static string? GetExecutablePath(Process p) => p.Safe(() => p.MainModule?.FileName);

        private static T Safe<T>(this Process p, Func<T> action, T defaultValue = default!)
        {
            try { return action(); } catch { return defaultValue; }
        }

        public static async Task<string> SaveProcessSnapshot(string? snapshotName = null)
        {
            var procs = GetCurrentProcesses(includeCpu: true);
            var snapshot = new ProcessSnapshot(
                DeviceId: Environment.MachineName,
                Timestamp: DateTime.UtcNow,
                SnapshotName: snapshotName ?? $"Processes_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                ProcessCount: procs.Count,
                Processes: procs
            );

            var dir = Path.Combine(DataPath, "ProcessSnapshots");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{snapshot.SnapshotName}.json");

            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(snapshot, options));

            return path;
        }

        public static List<string> GetSavedSnapshots()
        {
            var dir = Path.Combine(DataPath, "ProcessSnapshots");
            return Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*.json").Select(Path.GetFileNameWithoutExtension!).OrderByDescending(x => x).ToList()
                : new List<string>();
        }

        public static async Task<ProcessSnapshot?> LoadProcessSnapshot(string snapshotFileName)
        {
            var path = Path.Combine(DataPath, "ProcessSnapshots", $"{snapshotFileName}.json"); // Fixed: add .json if missing
            if (!File.Exists(path)) return null;

            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<ProcessSnapshot>(json);
        }
        // DETECT PROCESS TYPE (Steam, App, Game, System, etc)


        // HELPER METHODS

        private static float GetCpuUsage(Process process)
        {
            try
            {
                // This is a simplified version - for real CPU monitoring
                // you'd need to use PerformanceCounter or sample over time
                var startTime = DateTime.UtcNow;
                var startCpu = process.TotalProcessorTime;

                Thread.Sleep(100); // Sample over 100ms

                var endTime = DateTime.UtcNow;
                var endCpu = process.TotalProcessorTime;

                var cpuUsedMs = (endCpu - startCpu).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;

                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

                return (float)(cpuUsageTotal * 100);
            }
            catch
            {
                return 0;
            }
        }




        public static Dictionary<string, List<ProcessInfo>> GetProcessesByType()
        {
            var processes = GetCurrentProcesses();

            return processes
                .GroupBy(p => p.ProcessType)
                .ToDictionary(g => g.Key, g => g.ToList());
        }



        public static async Task<bool> KillProcess(int processId, string processName)
        {
            try
            {
                var process = Process.GetProcessById(processId);

                process.Kill();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to kill process {processId}: {ex.Message}");
                return false;
            }
        }
    }
}