using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace XRUIOS.Interpreter
{
    /// <summary>Spec pieces the BCL already answers the same way on every OS.</summary>
    internal static class Common
    {
        public static List<string> Disks()
        {
            var rows = new List<string>();
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady || d.TotalSize <= 0) continue;
                    if (d.DriveType is DriveType.Ram or DriveType.Unknown or DriveType.NoRootDirectory) continue;
                    // Skip Linux pseudo filesystems (proc, sysfs, tmpfs, overlay...).
                    if (!OperatingSystem.IsWindows() && d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Removable) continue;
                    rows.Add($"{d.Name}: {Gb(d.AvailableFreeSpace)} GB free of {Gb(d.TotalSize)} GB ({d.DriveFormat})");
                }
                catch { /* drive went away */ }
            }
            return rows;
        }

        public static List<string> Network()
        {
            var rows = new List<string>();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var ips = nic.GetIPProperties().UnicastAddresses
                    .Where(a => a.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                    .Where(a => !a.Address.IsIPv6LinkLocal)
                    .Select(a => a.Address.ToString())
                    .ToList();
                if (ips.Count > 0) rows.Add($"{nic.Name}: {string.Join(", ", ips)}");
            }
            return rows;
        }

        private static string Gb(long bytes) => Math.Round(bytes / 1024d / 1024 / 1024, 1).ToString("0.0");
    }
}
