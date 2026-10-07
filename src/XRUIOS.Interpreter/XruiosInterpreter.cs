using XRUIOS.Interpreter.Linux;
using XRUIOS.Interpreter.Windows;

namespace XRUIOS.Interpreter
{
    /// <summary>
    /// Entry point. Two layers:
    /// <list type="bullet">
    /// <item>The PLATFORM backend (Windows / Linux / Unsupported), picked from the OS - or replaced
    /// wholesale with <see cref="Use(IPlatformInterpreter?)"/>.</item>
    /// <item>Per-area OVERRIDES registered with <see cref="Use{T}"/>: a host swaps one area (say
    /// notifications become an in-world panel) and keeps the built-in OS behaviour for the rest. The
    /// same registry holds developer-owned interpreters (IThemeInterpreter, IAlarmInterpreter, ...)
    /// that have no built-in behaviour at all.</item>
    /// </list>
    /// <see cref="Current"/> always reflects both, including overrides registered later.
    /// </summary>
    public static class XruiosInterpreter
    {
        private static IPlatformInterpreter? _platform;
        private static readonly Dictionary<Type, object> Overrides = new();
        private static readonly object Gate = new();
        private static readonly LayeredInterpreter Layered = new();

        /// <summary>The platform backend with any per-area overrides applied.</summary>
        public static IPlatformInterpreter Current => Layered;

        /// <summary>The platform backend alone, without overrides.</summary>
        public static IPlatformInterpreter Platform
        {
            get
            {
                if (_platform is not null) return _platform;
                lock (Gate) return _platform ??= Detect();
            }
        }

        /// <summary>Replace the whole platform backend (Aether, a test double, ...). Null = auto-detect again.</summary>
        public static void Use(IPlatformInterpreter? interpreter)
        {
            lock (Gate) _platform = interpreter;
        }

        /// <summary>
        /// Register (or with null, remove) the interpreter for one area: a built-in one
        /// (<see cref="IAudioInterpreter"/>, <see cref="INotificationInterpreter"/>, ...) to override it,
        /// or a developer-owned one (<see cref="IThemeInterpreter"/>, <see cref="IAlarmInterpreter"/>, ...).
        /// </summary>
        public static void Use<T>(T? interpreter) where T : class
        {
            if (typeof(T) == typeof(IPlatformInterpreter))
                throw new ArgumentException("Use Use(IPlatformInterpreter) to replace the whole backend.");
            lock (Gate)
            {
                if (interpreter is null) Overrides.Remove(typeof(T));
                else Overrides[typeof(T)] = interpreter;
            }
        }

        /// <summary>The override for <typeparamref name="T"/> if one is registered, else the platform's own (or null).</summary>
        public static T? Get<T>() where T : class => GetOverride<T>() ?? FromPlatform<T>(Platform);

        /// <summary>Only an explicitly registered interpreter for <typeparamref name="T"/>, never the platform default.</summary>
        public static T? GetOverride<T>() where T : class
        {
            lock (Gate) return Overrides.TryGetValue(typeof(T), out var o) ? (T)o : null;
        }

        private static T? FromPlatform<T>(IPlatformInterpreter p) where T : class => typeof(T) switch
        {
            var t when t == typeof(IAudioInterpreter) => (T)p.Audio,
            var t when t == typeof(IPowerInterpreter) => (T)p.Power,
            var t when t == typeof(IClipboardInterpreter) => (T)p.Clipboard,
            var t when t == typeof(IAppInterpreter) => (T)p.Apps,
            var t when t == typeof(INotificationInterpreter) => (T)p.Notifications,
            var t when t == typeof(IKeyboardInterpreter) => (T)p.Keyboard,
            var t when t == typeof(IAppearanceInterpreter) => (T)p.Appearance,
            var t when t == typeof(ISystemInfoInterpreter) => (T)p.SystemInfo,
            _ => null,   // developer-owned areas have no platform default
        };

        /// <summary>What <see cref="Current"/> hands out: each area resolved through the override registry on every access.</summary>
        private sealed class LayeredInterpreter : IPlatformInterpreter
        {
            public InterpreterPlatform Platform => XruiosInterpreter.Platform.Platform;
            public IAudioInterpreter Audio => Get<IAudioInterpreter>()!;
            public IPowerInterpreter Power => Get<IPowerInterpreter>()!;
            public IClipboardInterpreter Clipboard => Get<IClipboardInterpreter>()!;
            public IAppInterpreter Apps => Get<IAppInterpreter>()!;
            public INotificationInterpreter Notifications => Get<INotificationInterpreter>()!;
            public IKeyboardInterpreter Keyboard => Get<IKeyboardInterpreter>()!;
            public IAppearanceInterpreter Appearance => Get<IAppearanceInterpreter>()!;
            public ISystemInfoInterpreter SystemInfo => Get<ISystemInfoInterpreter>()!;
        }

        public static IPlatformInterpreter Detect()
        {
            if (OperatingSystem.IsWindows()) return new WindowsInterpreter();
            if (OperatingSystem.IsLinux()) return new LinuxInterpreter();
            return new UnsupportedInterpreter();
        }
    }

    /// <summary>Every call fails with a clear message instead of silently doing nothing.</summary>
    public sealed class UnsupportedInterpreter : IPlatformInterpreter,
        IAudioInterpreter, IPowerInterpreter, IClipboardInterpreter, IAppInterpreter,
        INotificationInterpreter, IKeyboardInterpreter, IAppearanceInterpreter, ISystemInfoInterpreter
    {
        public InterpreterPlatform Platform => InterpreterPlatform.Unsupported;
        public IAudioInterpreter Audio => this;
        public IPowerInterpreter Power => this;
        public IClipboardInterpreter Clipboard => this;
        public IAppInterpreter Apps => this;
        public INotificationInterpreter Notifications => this;
        public IKeyboardInterpreter Keyboard => this;
        public IAppearanceInterpreter Appearance => this;
        public ISystemInfoInterpreter SystemInfo => this;

        private static Exception No() =>
            new PlatformNotSupportedException($"The XRUIOS interpreter has no backend for {System.Runtime.InteropServices.RuntimeInformation.OSDescription}.");

        public int GetMasterVolume() => throw No();
        public void SetMasterVolume(int volume0to100) => throw No();
        public bool GetMasterMute() => throw No();
        public void SetMasterMute(bool muted) => throw No();
        public IReadOnlyList<AudioChannel> ListChannels() => throw No();
        public void SetChannelVolume(string channelId, int volume0to100) => throw No();
        public void SetChannelMute(string channelId, bool muted) => throw No();
        public void Request(PowerAction action) => throw No();
        public BatteryStatus GetBattery() => throw No();
        public string? GetText() => throw No();
        public void SetText(string text) => throw No();
        public IReadOnlyList<InstalledApp> ListInstalled() => throw No();
        public int Launch(string appId) => throw No();
        public int LaunchRegistered(string entryPoint) => throw No();
        public void Show(string title, string body, string? source = null) => throw No();
        public IReadOnlyList<KeyboardLayout> ListLayouts() => throw No();
        public string? GetActiveLayout() => throw No();
        public void ApplyLayout(string layoutId) => throw No();
        public bool? GetDarkMode() => throw No();
        public void SetDarkMode(bool dark) => throw No();
        public void SetWallpaper(string imagePath) => throw No();
        public SystemSpecs GetSpecs() => throw No();
    }
}
