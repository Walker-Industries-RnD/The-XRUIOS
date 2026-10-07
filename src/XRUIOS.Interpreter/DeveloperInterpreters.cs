using System.Text.Json;
using System.Text.Json.Serialization;

namespace XRUIOS.Interpreter
{
    // ── Developer-owned domains ─────────────────────────────────────────────────────────────────
    //
    // The XRUIOS stores these settings and announces when they change, but what they MEAN depends on
    // the experience built on top: a desktop shell, a VR world, a game. So there is no built-in
    // behaviour - the host registers its own interpreter with XruiosInterpreter.Use<T>(...) and feeds
    // it events (InterpreterEvents.Dispatch). Unregistered = the setting is stored and nothing else.

    /// <summary>A theme was applied. Image paths are already resolved to absolute paths.</summary>
    public interface IThemeInterpreter { void Apply(ThemeInfo theme); }

    /// <summary>An alarm went off: ring it, flash it, vibrate a controller - the host decides.</summary>
    public interface IAlarmInterpreter { void Fired(AlarmInfo alarm); }

    /// <summary>
    /// Audio processing the OS mixer can't do on its own: per-category EQ (Voice/Music/Alerts/UI...) and
    /// the experimental settings (environmental reduction, decibel limit). Typically the host's own
    /// audio engine applies these to its buses.
    /// </summary>
    public interface IAudioEffectsInterpreter
    {
        void ApplyEq(EqSettings eq);
        void ApplyExperimental(ExperimentalAudioSettings settings);
    }

    /// <summary>
    /// Where weather comes from. The XRUIOS doesn't pick a weather service: a provider app runs one of
    /// these and pushes results in through the Geo worker's SetWeather capability (see
    /// <see cref="WeatherProvider"/>); every app then reads it with Current.
    /// </summary>
    public interface IWeatherSource { Task<WeatherSnapshot?> FetchAsync(string? city, CancellationToken ct); }

    /// <summary>
    /// The local assistant (PocketPal). Which model/service answers is the developer's call: a provider
    /// app runs one of these behind <c>AssistantProvider</c> (XRUIOS.Apps.Shared), which takes questions
    /// from the Assistant worker and posts the answers back.
    /// </summary>
    public interface IAssistantInterpreter { Task<string> AskAsync(string prompt, CancellationToken ct); }

    /// <summary>
    /// Opens an engine scene (an app id starting with "aether:"). Only an engine host implements this;
    /// see <see cref="AetherShellInterpreter"/>.
    /// </summary>
    public interface IShellAppInterpreter { void Open(string appId, string target); }

    // ── Event names + payloads (shared by the workers that publish and the hosts that interpret) ──

    /// <summary>
    /// The events the built-in workers publish. An app receives one only if it holds
    /// "Events.&lt;Name&gt;". Payloads are JSON of the records below.
    /// </summary>
    public static class XruiosEventNames
    {
        public const string ThemeChanged = "ThemeChanged";                     // ThemeInfo
        public const string NotificationPosted = "NotificationPosted";         // NotificationInfo
        public const string NotificationDismissed = "NotificationDismissed";   // { "id": ... }
        public const string VolumeChanged = "VolumeChanged";                   // VolumeChange
        public const string ExperimentalAudioChanged = "ExperimentalAudioChanged"; // ExperimentalAudioSettings
        public const string EqChanged = "EqChanged";                           // EqSettings
        public const string AlarmFired = "AlarmFired";                         // AlarmInfo
        public const string AlarmsChanged = "AlarmsChanged";                   // { }
        public const string WorldEventAdded = "WorldEventAdded";               // WorldEventInfo
        public const string WeatherUpdated = "WeatherUpdated";                 // WeatherSnapshot
        public const string KeyboardLayoutChanged = "KeyboardLayoutChanged";   // { "layoutId": ... }
        public const string ClipboardChanged = "ClipboardChanged";             // { "id": ..., "preview": ... }
        public const string AppLaunched = "AppLaunched";                       // AppLaunch
        public const string PowerRequested = "PowerRequested";                 // { "action": ... } - sent BEFORE acting
        public const string AssistantAsked = "AssistantAsked";                 // AssistantQuestion (to the provider app)
        public const string AssistantAnswered = "AssistantAnswered";           // AssistantAnswer
        public const string TimerFinished = "TimerFinished";                   // TimerInfo
        public const string NowPlayingChanged = "NowPlayingChanged";           // NowPlayingInfo (empty fields = nothing playing)

        // Presentation requests from workers running under the Aether shell (AetherShellInterpreter).
        // Only the engine host should hold these grants.
        public const string ShellNotify = "ShellNotify";                       // ShellNotification
        public const string ShellAppearance = "ShellAppearance";               // ShellAppearanceRequest
        public const string ShellLaunch = "ShellLaunch";                       // ShellLaunchRequest
    }

    public sealed record ThemeInfo(string Id, string Name, string? AccentColor, string? BackgroundColor, string? BackgroundImage);
    public sealed record AlarmInfo(string Name, DateTime Time, bool Recurring, string? SoundPath, int Volume);
    public sealed record EqSettings(string Name, float Software, float Effects, float Voice, float Music, float Alerts, float UI, float Etc);
    public sealed record ExperimentalAudioSettings(bool EnvironmentalReduction, int EnvironmentalReductionPercentage, bool DecibelLimit, int DecibelLimitLevel);
    public sealed record WeatherSnapshot(string Location, int TempC, string Condition, int High, int Low, DateTimeOffset At);
    public sealed record NotificationInfo(string Id, string Source, string Title, string Body, DateTimeOffset At);
    public sealed record VolumeChange(string ChannelId, int Volume, bool Muted);
    public sealed record WorldEventInfo(string Id, string Action, string Description, string Device, string? WorldPoint, DateTimeOffset At);
    public sealed record AppLaunch(string AppId, string Name, int Pid);
    public sealed record AssistantQuestion(string Id, string Prompt, string AskedBy, DateTimeOffset At);
    public sealed record AssistantAnswer(string Id, string Prompt, string Answer, DateTimeOffset At);
    public sealed record TimerInfo(string Name, int Seconds);
    public sealed record NowPlayingInfo(string SongId, string Title, string Artist, string Album, int DurationSeconds);
    public sealed record ShellNotification(string Title, string Body, string? Source);
    public sealed record ShellAppearanceRequest(bool? DarkMode, string? Wallpaper);
    public sealed record ShellLaunchRequest(string AppId, string Target);

    /// <summary>One JSON shape for every payload (camelCase), so hosts in any language can read them.</summary>
    public static class EventJson
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };

        public static string Write<T>(T payload) => JsonSerializer.Serialize(payload, Options);

        public static T Read<T>(string json) =>
            JsonSerializer.Deserialize<T>(json, Options) ?? throw new FormatException($"Empty {typeof(T).Name} payload.");
    }

    // ── Routing events to the registered interpreters (host side) ──────────────────────────────

    /// <summary>
    /// Hand every event from an XruiosEventStream to this and the right developer interpreter runs:
    /// <code>
    /// XruiosInterpreter.Use&lt;IThemeInterpreter&gt;(new MyVrThemes());
    /// await using var s = XruiosEventStream.Start(broker, appId, psk, e =&gt; InterpreterEvents.Dispatch(e.Name, e.Payload));
    /// </code>
    /// </summary>
    public static class InterpreterEvents
    {
        /// <summary>Events no registered interpreter handled (custom events, or no interpreter registered).</summary>
        public static event Action<string, string>? Unhandled;

        /// <returns>True when a registered interpreter handled it.</returns>
        public static bool Dispatch(string name, string payload)
        {
            bool handled = name switch
            {
                XruiosEventNames.ThemeChanged => Run<IThemeInterpreter>(i => i.Apply(EventJson.Read<ThemeInfo>(payload))),
                XruiosEventNames.AlarmFired => Run<IAlarmInterpreter>(i => i.Fired(EventJson.Read<AlarmInfo>(payload))),
                XruiosEventNames.EqChanged => Run<IAudioEffectsInterpreter>(i => i.ApplyEq(EventJson.Read<EqSettings>(payload))),
                XruiosEventNames.ExperimentalAudioChanged => Run<IAudioEffectsInterpreter>(i => i.ApplyExperimental(EventJson.Read<ExperimentalAudioSettings>(payload))),
                // Only an explicitly registered presenter: the Notification worker already raises the
                // OS toast by default, so falling back to the platform one would show it twice.
                XruiosEventNames.NotificationPosted => RunOverride<INotificationInterpreter>(i =>
                {
                    var n = EventJson.Read<NotificationInfo>(payload);
                    i.Show(n.Title, n.Body, n.Source);
                }),
                // Aether shell presentation requests: only a host that registered its own presenter.
                XruiosEventNames.ShellNotify => RunOverride<INotificationInterpreter>(i =>
                {
                    var n = EventJson.Read<ShellNotification>(payload);
                    i.Show(n.Title, n.Body, n.Source);
                }),
                XruiosEventNames.ShellAppearance => RunOverride<IAppearanceInterpreter>(i =>
                {
                    var a = EventJson.Read<ShellAppearanceRequest>(payload);
                    if (a.DarkMode is bool dark) i.SetDarkMode(dark);
                    if (!string.IsNullOrEmpty(a.Wallpaper)) i.SetWallpaper(a.Wallpaper);
                }),
                XruiosEventNames.ShellLaunch => RunOverride<IShellAppInterpreter>(i =>
                {
                    var l = EventJson.Read<ShellLaunchRequest>(payload);
                    i.Open(l.AppId, l.Target);
                }),
                _ => false,
            };
            if (!handled) Unhandled?.Invoke(name, payload);
            return handled;
        }

        private static bool Run<T>(Action<T> act) where T : class
        {
            var impl = XruiosInterpreter.Get<T>();
            if (impl is null) return false;
            act(impl);
            return true;
        }

        private static bool RunOverride<T>(Action<T> act) where T : class
        {
            var impl = XruiosInterpreter.GetOverride<T>();
            if (impl is null) return false;
            act(impl);
            return true;
        }
    }

    // ── Ready-made interpreters developers can opt into ─────────────────────────────────────────

    /// <summary>
    /// Mirrors a theme onto the host desktop: a dark background switches the OS to dark mode, and the
    /// theme's background image becomes the wallpaper. Opt-in (Theme worker "SetDesktopSync", or
    /// register it in your own host) - changing someone's desktop is a choice, not a default.
    /// </summary>
    public sealed class DesktopThemeInterpreter : IThemeInterpreter
    {
        /// <summary>What the last Apply did, e.g. "dark mode, wallpaper".</summary>
        public string LastResult { get; private set; } = "";

        public void Apply(ThemeInfo theme)
        {
            var notes = new List<string>();
            var appearance = XruiosInterpreter.Current.Appearance;

            if (Luminance(theme.BackgroundColor) is double lum)
            {
                try { appearance.SetDarkMode(lum < 0.5); notes.Add(lum < 0.5 ? "dark mode" : "light mode"); }
                catch (Exception ex) { notes.Add($"mode not set ({ex.Message})"); }
            }
            if (!string.IsNullOrEmpty(theme.BackgroundImage) && File.Exists(theme.BackgroundImage))
            {
                try { appearance.SetWallpaper(theme.BackgroundImage); notes.Add("wallpaper"); }
                catch (Exception ex) { notes.Add($"wallpaper not set ({ex.Message})"); }
            }
            LastResult = string.Join(", ", notes);
        }

        /// <summary>Perceived brightness 0..1 of "#RGB", "#RRGGBB" or "#RRGGBBAA"; null if unparseable.</summary>
        public static double? Luminance(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            string h = hex.Trim().TrimStart('#');
            if (h.Length == 3) h = string.Concat(h.Select(c => $"{c}{c}"));
            if (h.Length != 6 && h.Length != 8) return null;
            if (!int.TryParse(h[..2], System.Globalization.NumberStyles.HexNumber, null, out int r) ||
                !int.TryParse(h[2..4], System.Globalization.NumberStyles.HexNumber, null, out int g) ||
                !int.TryParse(h[4..6], System.Globalization.NumberStyles.HexNumber, null, out int b))
                return null;
            return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0;
        }
    }

    /// <summary>
    /// Runs an <see cref="IWeatherSource"/> on a timer and hands each result to <c>push</c> - in a
    /// provider app that's a call to the Geo worker's SetWeather capability.
    /// </summary>
    public sealed class WeatherProvider : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;

        public WeatherProvider(IWeatherSource source, Func<WeatherSnapshot, Task> push, TimeSpan every, string? city = null,
            Action<Exception>? onError = null)
        {
            _loop = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        var snap = await source.FetchAsync(city, _cts.Token);
                        if (snap != null) await push(snap);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex) { onError?.Invoke(ex); }
                    try { await Task.Delay(every, _cts.Token); } catch (OperationCanceledException) { break; }
                }
            });
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            try { await _loop; } catch { /* stopping */ }
            _cts.Dispose();
        }
    }
}
