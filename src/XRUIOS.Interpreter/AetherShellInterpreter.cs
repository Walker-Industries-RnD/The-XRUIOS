namespace XRUIOS.Interpreter
{
    /// <summary>
    /// The Aether Engine backend. When the XRUIOS runs with Aether as its shell, the engine is what the
    /// user sees, so the visual parts of the interpreter can't go to the desktop: a toast, a dark-mode
    /// switch or "open this scene" has to appear inside the engine instead. Workers are separate
    /// processes from the engine, so those areas become events (ShellNotify, ShellAppearance,
    /// ShellLaunch) that the engine-side host (XRUIOS.Aether's AetherShell) receives and renders.
    ///
    /// Everything the engine can't do for itself stays on the real OS underneath it: audio, power,
    /// clipboard, keyboard layouts and system info all pass straight through to the platform backend.
    ///
    /// Turned on per machine with XRUIOS_SHELL=aether in the Manager's environment; the workers inherit
    /// it and install this at startup.
    /// </summary>
    public sealed class AetherShellInterpreter : IPlatformInterpreter
    {
        public const string ShellEnv = "XRUIOS_SHELL";

        /// <summary>App ids / entry points with this prefix are engine scenes, not OS programs.</summary>
        public const string ScenePrefix = "aether:";

        public static bool IsRequested =>
            string.Equals(Environment.GetEnvironmentVariable(ShellEnv), "aether", StringComparison.OrdinalIgnoreCase);

        private readonly IPlatformInterpreter _os;

        /// <param name="os">The real platform underneath the engine (Windows or Linux).</param>
        /// <param name="publish">Sends an event (name, JSON payload) to the engine; in a worker that's WorkerEvents.Publish.</param>
        public AetherShellInterpreter(IPlatformInterpreter os, Action<string, string> publish)
        {
            _os = os;
            Notifications = new ShellNotifications(publish);
            Appearance = new ShellAppearance(publish);
            Apps = new ShellApps(os.Apps, publish);
        }

        public InterpreterPlatform Platform => InterpreterPlatform.Aether;
        public IAudioInterpreter Audio => _os.Audio;
        public IPowerInterpreter Power => _os.Power;
        public IClipboardInterpreter Clipboard => _os.Clipboard;
        public IKeyboardInterpreter Keyboard => _os.Keyboard;
        public ISystemInfoInterpreter SystemInfo => _os.SystemInfo;
        public INotificationInterpreter Notifications { get; }
        public IAppearanceInterpreter Appearance { get; }
        public IAppInterpreter Apps { get; }

        private sealed class ShellNotifications(Action<string, string> publish) : INotificationInterpreter
        {
            public void Show(string title, string body, string? source = null) =>
                publish(XruiosEventNames.ShellNotify, EventJson.Write(new ShellNotification(title, body, source)));
        }

        // The engine owns light/dark and the backdrop. We remember the last request so GetDarkMode
        // answers with what the shell was told, not with the desktop hidden behind it.
        private sealed class ShellAppearance(Action<string, string> publish) : IAppearanceInterpreter
        {
            private bool? _dark;

            public bool? GetDarkMode() => _dark;

            public void SetDarkMode(bool dark)
            {
                _dark = dark;
                publish(XruiosEventNames.ShellAppearance, EventJson.Write(new ShellAppearanceRequest(dark, null)));
            }

            public void SetWallpaper(string imagePath) =>
                publish(XruiosEventNames.ShellAppearance, EventJson.Write(new ShellAppearanceRequest(null, imagePath)));
        }

        // OS programs still start on the OS (a desktop app can open in an engine window); ids marked
        // "aether:" are scenes, which only the engine can open.
        private sealed class ShellApps(IAppInterpreter os, Action<string, string> publish) : IAppInterpreter
        {
            public IReadOnlyList<InstalledApp> ListInstalled() => os.ListInstalled();

            public int Launch(string appId) => IsScene(appId) ? Open(appId, appId) : os.Launch(appId);

            public int LaunchRegistered(string entryPoint) =>
                IsScene(entryPoint) ? Open(entryPoint, entryPoint) : os.LaunchRegistered(entryPoint);

            private int Open(string appId, string target)
            {
                publish(XruiosEventNames.ShellLaunch, EventJson.Write(new ShellLaunchRequest(appId, target[ScenePrefix.Length..])));
                return 0;
            }

            private static bool IsScene(string id) => id.StartsWith(ScenePrefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
