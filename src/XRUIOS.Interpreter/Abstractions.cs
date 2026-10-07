namespace XRUIOS.Interpreter
{
    // The contract every platform backend implements. The system classes (src/systems) own the DATA -
    // what the user saved - and the interpreter owns the EFFECT: what that data means on the machine
    // the worker is running on. Windows, Linux and the Aether Engine each supply one implementation;
    // workers only ever talk to these interfaces through XruiosInterpreter.Current.

    public enum InterpreterPlatform { Windows, Linux, Aether, Unsupported }

    public interface IPlatformInterpreter
    {
        InterpreterPlatform Platform { get; }
        IAudioInterpreter Audio { get; }
        IPowerInterpreter Power { get; }
        IClipboardInterpreter Clipboard { get; }
        IAppInterpreter Apps { get; }
        INotificationInterpreter Notifications { get; }
        IKeyboardInterpreter Keyboard { get; }
        IAppearanceInterpreter Appearance { get; }
        ISystemInfoInterpreter SystemInfo { get; }
    }

    // ── Audio ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One mixer row. <see cref="AudioChannel.MasterId"/> is the default output device; every other
    /// row is a per-app stream (a Windows audio session / a PulseAudio sink-input).
    /// </summary>
    public sealed record AudioChannel(string Id, string Name, int Volume, bool Muted)
    {
        public const string MasterId = "master";
    }

    public interface IAudioInterpreter
    {
        int GetMasterVolume();
        void SetMasterVolume(int volume0to100);
        bool GetMasterMute();
        void SetMasterMute(bool muted);

        /// <summary>Master first, then every app currently producing audio.</summary>
        IReadOnlyList<AudioChannel> ListChannels();
        void SetChannelVolume(string channelId, int volume0to100);
        void SetChannelMute(string channelId, bool muted);
    }

    // ── Power ────────────────────────────────────────────────────────────────────────────────────

    public enum PowerAction { Shutdown, Restart, Sleep, Hibernate, SignOut, Lock }

    /// <param name="Percent">0-100, or -1 when unknown / no battery.</param>
    /// <param name="Remaining">Estimated time left on battery, when the OS reports one.</param>
    public sealed record BatteryStatus(bool HasBattery, int Percent, bool Charging, bool PluggedIn, TimeSpan? Remaining);

    public interface IPowerInterpreter
    {
        void Request(PowerAction action);
        BatteryStatus GetBattery();
    }

    // ── Clipboard ────────────────────────────────────────────────────────────────────────────────

    public interface IClipboardInterpreter
    {
        /// <summary>The OS clipboard's text, or null when it's empty / not text.</summary>
        string? GetText();
        void SetText(string text);
    }

    // ── Apps ─────────────────────────────────────────────────────────────────────────────────────

    /// <param name="Id">Stable id to hand back to <see cref="IAppInterpreter.Launch"/>.</param>
    /// <param name="Target">What actually gets started (shortcut / .desktop file / executable).</param>
    public sealed record InstalledApp(string Id, string Name, string IconPath, string Target);

    public interface IAppInterpreter
    {
        IReadOnlyList<InstalledApp> ListInstalled();

        /// <summary>
        /// Start an app from <see cref="ListInstalled"/> by id. Deliberately NOT a "run any path"
        /// call - a capability that can start arbitrary executables would be a hole in the permission
        /// model. Returns the new process id, or 0 when the OS doesn't report one.
        /// </summary>
        int Launch(string appId);

        /// <summary>Start a file the XRUIOS itself registered (an app manifest's EntryPoint).</summary>
        int LaunchRegistered(string entryPoint);
    }

    // ── Notifications ────────────────────────────────────────────────────────────────────────────

    public interface INotificationInterpreter
    {
        void Show(string title, string body, string? source = null);
    }

    // ── Keyboard layouts ─────────────────────────────────────────────────────────────────────────

    public sealed record KeyboardLayout(string Id, string DisplayName);

    public interface IKeyboardInterpreter
    {
        IReadOnlyList<KeyboardLayout> ListLayouts();
        string? GetActiveLayout();
        void ApplyLayout(string layoutId);
    }

    // ── Appearance ───────────────────────────────────────────────────────────────────────────────

    public interface IAppearanceInterpreter
    {
        /// <summary>True = dark, false = light, null = the desktop doesn't expose it.</summary>
        bool? GetDarkMode();
        void SetDarkMode(bool dark);
        void SetWallpaper(string imagePath);
    }

    // ── System info ──────────────────────────────────────────────────────────────────────────────

    public sealed record SystemSpecs(
        string OS,
        string Cpu,
        int CpuCores,
        long MemoryTotalMB,
        long MemoryFreeMB,
        IReadOnlyList<string> Disks,
        IReadOnlyList<string> Gpus,
        IReadOnlyList<string> Network,
        TimeSpan Uptime);

    public interface ISystemInfoInterpreter
    {
        SystemSpecs GetSpecs();
    }
}
