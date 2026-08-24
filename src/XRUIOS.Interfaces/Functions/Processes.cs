

namespace XRUIOS.Barebones.Interfaces
{
    public static class ProcessesClass
    {
        private const int MaxProcessHistory = 100;
        private static readonly TimeSpan CpuSampleInterval = TimeSpan.FromMilliseconds(500);

        public record ProcessInfo(
            string DeviceId,
            int ProcessId,
            string ProcessName,
            string ProcessType,
            string? WindowTitle = null,
            DateTime? StartTime = null,
            long MemoryMB = 0,
            float CpuPercent = 0,
            string? ExecutablePath = null,
            string? IconPath = null,
            bool IsResponsive = true,
            DateTime LastSeen = default)
        {
            public ProcessInfo() : this(
                Environment.MachineName,
                0,
                string.Empty,
                "Unknown",
                null,
                DateTime.UtcNow,
                0,
                0,
                null,
                null,
                true,
                DateTime.UtcNow)
            { }
        }

        public record ProcessSnapshot(
            string DeviceId,
            DateTime Timestamp,
            string SnapshotName,
            int ProcessCount,
            List<ProcessInfo> Processes);

  
    }
}