using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using Microsoft.Win32;

namespace XRUIOS.Interpreter.Windows
{
    [SupportedOSPlatform("windows")]
    public sealed class WindowsInterpreter : IPlatformInterpreter
    {
        public InterpreterPlatform Platform => InterpreterPlatform.Windows;
        public IAudioInterpreter Audio { get; } = new WindowsAudio();
        public IPowerInterpreter Power { get; } = new WindowsPower();
        public IClipboardInterpreter Clipboard { get; } = new WindowsClipboard();
        public IAppInterpreter Apps { get; } = new WindowsApps();
        public INotificationInterpreter Notifications { get; } = new WindowsNotifications();
        public IKeyboardInterpreter Keyboard { get; } = new WindowsKeyboard();
        public IAppearanceInterpreter Appearance { get; } = new WindowsAppearance();
        public ISystemInfoInterpreter SystemInfo { get; } = new WindowsSystemInfo();
    }

    // ── Power ────────────────────────────────────────────────────────────────────────────────────

    [SupportedOSPlatform("windows")]
    internal sealed class WindowsPower : IPowerInterpreter
    {
        private const uint EWX_SHUTDOWN = 0x1, EWX_REBOOT = 0x2, EWX_POWEROFF = 0x8;

        public void Request(PowerAction action)
        {
            switch (action)
            {
                case PowerAction.Shutdown:
                    EnableShutdownPrivilege();
                    if (!Native.ExitWindowsEx(EWX_SHUTDOWN | EWX_POWEROFF, Native.SHTDN_REASON_FLAG_PLANNED))
                        throw Native.LastError("ExitWindowsEx(shutdown)");
                    break;
                case PowerAction.Restart:
                    EnableShutdownPrivilege();
                    if (!Native.ExitWindowsEx(EWX_REBOOT, Native.SHTDN_REASON_FLAG_PLANNED))
                        throw Native.LastError("ExitWindowsEx(restart)");
                    break;
                case PowerAction.Sleep:
                case PowerAction.Hibernate:
                    EnableShutdownPrivilege();
                    if (!Native.SetSuspendState(action == PowerAction.Hibernate, false, false))
                        throw Native.LastError(action == PowerAction.Hibernate ? "SetSuspendState(hibernate)" : "SetSuspendState(sleep)");
                    break;
                case PowerAction.SignOut:
                    if (!Native.ExitWindowsEx(Native.EWX_LOGOFF, Native.SHTDN_REASON_FLAG_PLANNED))
                        throw Native.LastError("ExitWindowsEx(sign out)");
                    break;
                case PowerAction.Lock:
                    // Only works from a process on the interactive desktop (not a session-0 service).
                    if (!Native.LockWorkStation()) throw Native.LastError("LockWorkStation");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        public BatteryStatus GetBattery()
        {
            if (!Native.GetSystemPowerStatus(out var s)) throw Native.LastError("GetSystemPowerStatus");

            bool noBattery = (s.BatteryFlag & 128) != 0 || s.BatteryFlag == 255;
            return new BatteryStatus(
                HasBattery: !noBattery,
                Percent: s.BatteryLifePercent == 255 ? -1 : s.BatteryLifePercent,
                Charging: !noBattery && (s.BatteryFlag & 8) != 0,
                PluggedIn: s.ACLineStatus == 1,
                Remaining: s.BatteryLifeTime < 0 ? null : TimeSpan.FromSeconds(s.BatteryLifeTime));
        }

        private static void EnableShutdownPrivilege()
        {
            if (!Native.OpenProcessToken(Native.GetCurrentProcess(), Native.TOKEN_ADJUST_PRIVILEGES | Native.TOKEN_QUERY, out var token))
                throw Native.LastError("OpenProcessToken");
            try
            {
                if (!Native.LookupPrivilegeValue(null, "SeShutdownPrivilege", out var luid))
                    throw Native.LastError("LookupPrivilegeValue");
                var tp = new Native.TOKEN_PRIVILEGES { PrivilegeCount = 1, Luid = luid, Attributes = Native.SE_PRIVILEGE_ENABLED };
                if (!Native.AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
                    throw Native.LastError("AdjustTokenPrivileges");
            }
            finally { Native.CloseHandle(token); }
        }
    }

    // ── Clipboard ────────────────────────────────────────────────────────────────────────────────

    [SupportedOSPlatform("windows")]
    internal sealed class WindowsClipboard : IClipboardInterpreter
    {
        public string? GetText()
        {
            if (!Native.IsClipboardFormatAvailable(Native.CF_UNICODETEXT)) return null;
            using var _ = Open();
            var handle = Native.GetClipboardData(Native.CF_UNICODETEXT);
            if (handle == IntPtr.Zero) return null;
            var ptr = Native.GlobalLock(handle);
            if (ptr == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringUni(ptr); }
            finally { Native.GlobalUnlock(handle); }
        }

        public void SetText(string text)
        {
            using var _ = Open();
            if (!Native.EmptyClipboard()) throw Native.LastError("EmptyClipboard");

            byte[] bytes = Encoding.Unicode.GetBytes(text + "\0");
            var hGlobal = Native.GlobalAlloc(Native.GMEM_MOVEABLE, (UIntPtr)bytes.Length);
            if (hGlobal == IntPtr.Zero) throw Native.LastError("GlobalAlloc");

            var target = Native.GlobalLock(hGlobal);
            if (target == IntPtr.Zero) { Native.GlobalFree(hGlobal); throw Native.LastError("GlobalLock"); }
            try { Marshal.Copy(bytes, 0, target, bytes.Length); }
            finally { Native.GlobalUnlock(hGlobal); }

            // On success the clipboard owns the memory; only free it ourselves on failure.
            if (Native.SetClipboardData(Native.CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
            {
                Native.GlobalFree(hGlobal);
                throw Native.LastError("SetClipboardData");
            }
        }

        /// <summary>The clipboard is a global lock another app may be holding for a moment; retry briefly.</summary>
        private static Closer Open()
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (Native.OpenClipboard(IntPtr.Zero)) return new Closer();
                Thread.Sleep(20);
            }
            throw Native.LastError("OpenClipboard");
        }

        private readonly struct Closer : IDisposable
        {
            public void Dispose() => Native.CloseClipboard();
        }
    }

    // ── Notifications ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Native Windows toasts. A toast must come from a registered AppUserModelID; until the XRUIOS
    /// installer registers its own, it borrows Windows PowerShell's (always present on Windows 10/11).
    /// Runs Windows PowerShell 5.1 (not pwsh - only 5.1 can load the WinRT toast types) with the
    /// script passed encoded, and every piece of app text XML-escaped, so nothing is ever evaluated.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class WindowsNotifications : INotificationInterpreter
    {
        private const string PowerShellAumid = @"{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\WindowsPowerShell\v1.0\powershell.exe";

        public void Show(string title, string body, string? source = null)
        {
            string attribution = string.IsNullOrWhiteSpace(source) ? "" : $"<text placement=\"attribution\">{Xml(source)}</text>";
            string toastXml =
                "<toast><visual><binding template=\"ToastGeneric\">" +
                $"<text>{Xml(title)}</text><text>{Xml(body)}</text>{attribution}" +
                "</binding></visual></toast>";

            string script =
                "$ErrorActionPreference='Stop';" +
                "[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null;" +
                "[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] > $null;" +
                "$x = New-Object Windows.Data.Xml.Dom.XmlDocument;" +
                $"$x.LoadXml('{toastXml}');" +
                $"[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('{PowerShellAumid}').Show([Windows.UI.Notifications.ToastNotification]::new($x))";

            string powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            Shell.RunChecked(powershell,
                new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-EncodedCommand", encoded },
                timeoutMs: 20000);
        }

        // SecurityElement.Escape also turns ' into &apos;, so the text can't break out of the PS string.
        private static string Xml(string s) => SecurityElement.Escape(s) ?? "";
    }

    // ── Appearance ───────────────────────────────────────────────────────────────────────────────

    [SupportedOSPlatform("windows")]
    internal sealed class WindowsAppearance : IAppearanceInterpreter
    {
        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public bool? GetDarkMode()
        {
            using var k = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return k?.GetValue("AppsUseLightTheme") is int light ? light == 0 : null;
        }

        public void SetDarkMode(bool dark)
        {
            using var k = Registry.CurrentUser.CreateSubKey(PersonalizeKey, writable: true);
            k.SetValue("AppsUseLightTheme", dark ? 0 : 1, RegistryValueKind.DWord);
            k.SetValue("SystemUsesLightTheme", dark ? 0 : 1, RegistryValueKind.DWord);
            // Tell running apps + the shell to re-read the colour set now instead of at next sign-in.
            Native.SendMessageTimeout(Native.HWND_BROADCAST, Native.WM_SETTINGCHANGE, IntPtr.Zero,
                "ImmersiveColorSet", Native.SMTO_ABORTIFHUNG, 1000, out _);
        }

        public void SetWallpaper(string imagePath)
        {
            string full = Path.GetFullPath(imagePath);
            if (!File.Exists(full)) throw new FileNotFoundException("Wallpaper image not found.", full);
            if (!Native.SystemParametersInfoString(Native.SPI_SETDESKWALLPAPER, 0, full, Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE))
                throw Native.LastError("SystemParametersInfo(SPI_SETDESKWALLPAPER)");
        }
    }
}
