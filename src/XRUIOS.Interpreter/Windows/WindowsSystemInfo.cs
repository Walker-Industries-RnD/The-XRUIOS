using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace XRUIOS.Interpreter.Windows
{
    /// <summary>
    /// Machine specs without WMI (System.Management is slow to spin up and not available to every
    /// host): registry for the CPU name, GlobalMemoryStatusEx for RAM, EnumDisplayDevices for GPUs.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class WindowsSystemInfo : ISystemInfoInterpreter
    {
        public SystemSpecs GetSpecs()
        {
            var mem = new Native.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
            Native.GlobalMemoryStatusEx(ref mem);

            return new SystemSpecs(
                OS: OsName(),
                Cpu: CpuName(),
                CpuCores: Environment.ProcessorCount,
                MemoryTotalMB: (long)(mem.ullTotalPhys / 1024 / 1024),
                MemoryFreeMB: (long)(mem.ullAvailPhys / 1024 / 1024),
                Disks: Common.Disks(),
                Gpus: Gpus(),
                Network: Common.Network(),
                Uptime: TimeSpan.FromMilliseconds(Environment.TickCount64));
        }

        private static string OsName()
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                string? product = k?.GetValue("ProductName") as string;
                string? display = k?.GetValue("DisplayVersion") as string;
                string build = Environment.OSVersion.Version.Build.ToString();
                // ProductName still says "Windows 10" on Windows 11; the build number is the tell.
                if (product != null && Environment.OSVersion.Version.Build >= 22000)
                    product = product.Replace("Windows 10", "Windows 11");
                if (product != null) return $"{product} {display} (build {build})".Replace("  ", " ");
            }
            catch { /* fall through */ }
            return RuntimeInformation.OSDescription;
        }

        private static string CpuName()
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (k?.GetValue("ProcessorNameString") is string s && s.Trim().Length > 0) return s.Trim();
            }
            catch { /* fall through */ }
            return RuntimeInformation.ProcessArchitecture.ToString();
        }

        private static List<string> Gpus()
        {
            var names = new List<string>();
            var dd = new Native.DISPLAY_DEVICE { cb = Marshal.SizeOf<Native.DISPLAY_DEVICE>() };
            for (uint i = 0; Native.EnumDisplayDevices(null, i, ref dd, 0); i++)
            {
                if (!string.IsNullOrWhiteSpace(dd.DeviceString) && !names.Contains(dd.DeviceString))
                    names.Add(dd.DeviceString);
                dd.cb = Marshal.SizeOf<Native.DISPLAY_DEVICE>();
            }
            return names;
        }
    }
}
