using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace XRUIOS.Interpreter.Windows
{
    [SupportedOSPlatform("windows")]
    internal static class Native
    {
        // ── clipboard ──
        public const uint CF_UNICODETEXT = 13;
        public const uint GMEM_MOVEABLE = 0x0002;

        [DllImport("user32.dll", SetLastError = true)] public static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool CloseClipboard();
        [DllImport("user32.dll", SetLastError = true)] public static extern bool EmptyClipboard();
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
        [DllImport("user32.dll")] public static extern bool IsClipboardFormatAvailable(uint format);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GlobalUnlock(IntPtr hMem);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr GlobalFree(IntPtr hMem);

        // ── power ──
        public const uint EWX_LOGOFF = 0x00000000;
        public const uint SHTDN_REASON_FLAG_PLANNED = 0x80000000;

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;        // 0 offline, 1 online, 255 unknown
            public byte BatteryFlag;         // 8 charging, 128 no battery, 255 unknown
            public byte BatteryLifePercent;  // 255 unknown
            public byte SystemStatusFlag;
            public int BatteryLifeTime;      // seconds, -1 unknown
            public int BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool ExitWindowsEx(uint uFlags, uint dwReason);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool LockWorkStation();
        [DllImport("powrprof.dll", SetLastError = true)] public static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        // SeShutdownPrivilege - required by SetSuspendState (and shutdown/restart).
        public const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        public const uint TOKEN_QUERY = 0x0008;
        public const uint SE_PRIVILEGE_ENABLED = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        public struct TOKEN_PRIVILEGES { public uint PrivilegeCount; public LUID Luid; public uint Attributes; }

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LookupPrivilegeValue(string? systemName, string name, out LUID luid);
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAll, ref TOKEN_PRIVILEGES newState,
            uint bufferLength, IntPtr previousState, IntPtr returnLength);
        [DllImport("kernel32.dll")] public static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool CloseHandle(IntPtr handle);

        // ── keyboard ──
        public const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;
        public const uint SPI_SETDEFAULTINPUTLANG = 0x005A;
        public const uint SPI_SETDESKWALLPAPER = 0x0014;
        public const uint SPIF_UPDATEINIFILE = 0x01;
        public const uint SPIF_SENDCHANGE = 0x02;
        public static readonly IntPtr HWND_BROADCAST = new(0xffff);

        [DllImport("user32.dll")] public static extern int GetKeyboardLayoutList(int nBuff, [Out] IntPtr[]? lpList);
        [DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint idThread);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SystemParametersInfo(uint action, uint param, ref IntPtr vparam, uint winIni);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "SystemParametersInfoW")]
        public static extern bool SystemParametersInfoString(uint action, uint param, string vparam, uint winIni);

        // ── appearance ──
        public const uint WM_SETTINGCHANGE = 0x001A;
        public const uint SMTO_ABORTIFHUNG = 0x0002;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam,
            uint flags, uint timeout, out IntPtr result);

        // ── system info ──
        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplayDevices(string? device, uint devNum, ref DISPLAY_DEVICE displayDevice, uint flags);

        public static Exception LastError(string what) =>
            new InvalidOperationException($"{what} failed (Win32 error {Marshal.GetLastWin32Error()}).");
    }
}
