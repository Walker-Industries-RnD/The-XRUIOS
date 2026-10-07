using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace XRUIOS.Interpreter.Linux
{
    [SupportedOSPlatform("linux")]
    internal sealed class LinuxSystemInfo : ISystemInfoInterpreter
    {
        public SystemSpecs GetSpecs()
        {
            var mem = MemInfo();
            return new SystemSpecs(
                OS: OsName(),
                Cpu: CpuName(),
                CpuCores: Environment.ProcessorCount,
                MemoryTotalMB: mem.GetValueOrDefault("MemTotal") / 1024,
                MemoryFreeMB: mem.GetValueOrDefault("MemAvailable", mem.GetValueOrDefault("MemFree")) / 1024,
                Disks: Common.Disks(),
                Gpus: Gpus(),
                Network: Common.Network(),
                Uptime: Uptime());
        }

        private static string OsName()
        {
            try
            {
                foreach (var line in File.ReadLines("/etc/os-release"))
                    if (line.StartsWith("PRETTY_NAME=")) return line[12..].Trim('"') + $" (kernel {Environment.OSVersion.Version})";
            }
            catch { /* fall through */ }
            return RuntimeInformation.OSDescription;
        }

        private static string CpuName()
        {
            try
            {
                foreach (var line in File.ReadLines("/proc/cpuinfo"))
                {
                    // x86: "model name"; ARM boards: "Model" / "Hardware".
                    if (line.StartsWith("model name") || line.StartsWith("Model") || line.StartsWith("Hardware"))
                    {
                        int c = line.IndexOf(':');
                        if (c > 0 && line[(c + 1)..].Trim() is { Length: > 0 } v) return v;
                    }
                }
            }
            catch { /* fall through */ }
            return RuntimeInformation.ProcessArchitecture.ToString();
        }

        /// <summary>/proc/meminfo in kB.</summary>
        private static Dictionary<string, long> MemInfo()
        {
            var d = new Dictionary<string, long>();
            try
            {
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    int c = line.IndexOf(':');
                    if (c <= 0) continue;
                    var num = line[(c + 1)..].Trim().Split(' ')[0];
                    if (long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out long kb)) d[line[..c]] = kb;
                }
            }
            catch { /* empty */ }
            return d;
        }

        private static List<string> Gpus()
        {
            var names = new List<string>();
            if (Shell.Has("lspci"))
            {
                var r = Shell.Run("lspci", Array.Empty<string>(), cLocale: true);
                if (r.Ok)
                    foreach (var line in r.StdOut.Split('\n'))
                        if (line.Contains("VGA compatible controller") || line.Contains("3D controller") || line.Contains("Display controller"))
                        {
                            int c = line.IndexOf(": ", line.IndexOf("controller", StringComparison.Ordinal), StringComparison.Ordinal);
                            if (c > 0) names.Add(line[(c + 2)..].Trim());
                        }
            }
            if (names.Count > 0) return names;

            // No pciutils (or an SoC): report the DRM driver of each card.
            try
            {
                foreach (var card in Directory.EnumerateDirectories("/sys/class/drm", "card?"))
                {
                    var uevent = Path.Combine(card, "device", "uevent");
                    if (!File.Exists(uevent)) continue;
                    var driver = File.ReadLines(uevent).FirstOrDefault(l => l.StartsWith("DRIVER="))?[7..];
                    if (driver != null && !names.Contains(driver)) names.Add(driver);
                }
            }
            catch { /* no drm */ }
            return names;
        }

        private static TimeSpan Uptime()
        {
            try
            {
                var first = File.ReadAllText("/proc/uptime").Split(' ')[0];
                return TimeSpan.FromSeconds(double.Parse(first, CultureInfo.InvariantCulture));
            }
            catch { return TimeSpan.FromMilliseconds(Environment.TickCount64); }
        }
    }
}
