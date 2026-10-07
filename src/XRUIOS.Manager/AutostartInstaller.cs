using System.Diagnostics;

namespace XRUIOS.Manager
{
    // Registers the Manager to start automatically. Windows gets two options, Linux gets a systemd unit:
    //   install / uninstall                 - a Scheduled Task at user logon (runs `start`, user session,
    //                                         can see the desktop and create OS accounts).
    //   install-service / uninstall-service - a Windows Service at boot (runs `run-service`, session 0,
    //                                         needs an elevated shell).
    // On Linux, install writes a systemd --user unit that runs `run` and enables it.
    internal static class AutostartInstaller
    {
        private const string TaskName = "XRUIOS.Manager";
        private const string ServiceName = "XRUIOS.Manager";

        private static string Exe => Environment.ProcessPath ?? "XRUIOS.Manager";

        public static int Install()
        {
            if (OperatingSystem.IsWindows())
                return Run("schtasks",
                    $"/Create /TN \"{TaskName}\" /TR \"\\\"{Exe}\\\" start\" /SC ONLOGON /RL HIGHEST /F",
                    "Registered logon autostart.");
            if (OperatingSystem.IsLinux())
                return InstallSystemd();
            Console.Error.WriteLine("Autostart is not supported on this platform.");
            return 1;
        }

        public static int Uninstall()
        {
            if (OperatingSystem.IsWindows())
                return Run("schtasks", $"/Delete /TN \"{TaskName}\" /F", "Removed logon autostart.");
            if (OperatingSystem.IsLinux())
                return Run("systemctl", "--user disable --now xruios.service", "Disabled systemd unit.");
            return 1;
        }

        public static int InstallService()
        {
            if (!OperatingSystem.IsWindows()) { Console.Error.WriteLine("Windows only (use `install` on Linux)."); return 1; }
            return Run("sc",
                $"create \"{ServiceName}\" binPath= \"\\\"{Exe}\\\" run-service\" start= auto DisplayName= \"XRUIOS Manager\"",
                "Installed Windows Service. Start it with `sc start XRUIOS.Manager` (elevated).");
        }

        public static int UninstallService()
        {
            if (!OperatingSystem.IsWindows()) { Console.Error.WriteLine("Windows only."); return 1; }
            Run("sc", $"stop \"{ServiceName}\"", null);
            return Run("sc", $"delete \"{ServiceName}\"", "Removed Windows Service.");
        }

        private static int InstallSystemd()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "systemd", "user");
            Directory.CreateDirectory(dir);

            string unit =
                "[Unit]\n" +
                "Description=XRUIOS Manager\n\n" +
                "[Service]\n" +
                $"ExecStart={Exe} run\n" +
                "Restart=on-failure\n\n" +
                "[Install]\n" +
                "WantedBy=default.target\n";
            File.WriteAllText(Path.Combine(dir, "xruios.service"), unit);

            Run("systemctl", "--user daemon-reload", null);
            return Run("systemctl", "--user enable --now xruios.service", "Installed and enabled systemd unit.");
        }

        private static int Run(string file, string args, string? okMessage)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = false });
                p!.WaitForExit();
                if (p.ExitCode == 0)
                {
                    if (okMessage != null) Console.WriteLine(okMessage);
                    return 0;
                }
                Console.Error.WriteLine($"{file} exited with code {p.ExitCode} (elevation may be required).");
                return p.ExitCode;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to run {file}: {ex.Message}");
                return 1;
            }
        }
    }
}
