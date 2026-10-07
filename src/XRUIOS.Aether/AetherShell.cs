using XRUIOS.Interfaces;
using XRUIOS.Interpreter;

namespace XRUIOS.Aether
{
    /// <summary>
    /// The engine-side half of the Aether backend. The engine (a Godot / Aether Engine game acting as the
    /// XRUIOS shell) connects to the Manager as a normal app, receives the events it's granted, and
    /// hands each one to the interpreters it registered:
    /// <list type="bullet">
    ///   <item>ShellNotify / ShellAppearance / ShellLaunch, sent by workers running with XRUIOS_SHELL=aether
    ///   (see <see cref="AetherShellInterpreter"/>), go to the engine's INotificationInterpreter,
    ///   IAppearanceInterpreter and IShellAppInterpreter.</item>
    ///   <item>ThemeChanged, AlarmFired, EqChanged and the other developer-owned events go to its
    ///   IThemeInterpreter, IAlarmInterpreter, IAudioEffectsInterpreter, ...</item>
    /// </list>
    /// Godot's scene tree may only be touched from the main thread, so every callback goes through
    /// <c>post</c>; in a Node that's <c>a =&gt; Callable.From(a).CallDeferred()</c>.
    /// </summary>
    public sealed class AetherShell : IAsyncDisposable
    {
        private readonly EclipseSecureClient _client;
        private readonly XruiosEventStream _events;

        public string AppId { get; }

        /// <summary>Events no registered interpreter handled (custom events, or nothing registered).</summary>
        public event Action<XruiosEvent>? Unhandled;

        private AetherShell(EclipseSecureClient client, string appId, string broker, byte[] psk,
            Action<Action> post, Action<Exception>? onError)
        {
            _client = client;
            AppId = appId;
            _events = XruiosEventStream.Start(broker, appId, psk, e => post(() =>
            {
                if (!InterpreterEvents.Dispatch(e.Name, e.Payload)) Unhandled?.Invoke(e);
            }), onError);
        }

        /// <summary>
        /// Connect as <paramref name="appName"/>. Credentials come from the environment when the Manager
        /// spawned the engine (XRUIOS_APP_ID / XRUIOS_APP_PSK / XRUIOS_BROKER_ADDR), otherwise from
        /// attested enrollment: the Manager checks this process is the registered engine executable.
        /// Register interpreters (XruiosInterpreter.Use&lt;T&gt;) before or after; dispatch looks them up per event.
        /// </summary>
        public static async Task<AetherShell> ConnectAsync(string appName, Action<Action> post, Action<Exception>? onError = null)
        {
            string? id = Environment.GetEnvironmentVariable("XRUIOS_APP_ID");
            string? psk = Environment.GetEnvironmentVariable("XRUIOS_APP_PSK");
            string? broker = Environment.GetEnvironmentVariable("XRUIOS_BROKER_ADDR");
            if (id is null || psk is null || broker is null)
            {
                var p = await XruiosEnrollment.EnrollAsync(appName);
                (id, psk, broker) = (p.AppId, p.PskBase64, p.BrokerAddress);
            }

            byte[] key = Convert.FromBase64String(psk);
            var client = await EclipseSecureClient.ConnectAsync(broker, appName, key, identity: id);
            return new AetherShell(client, id, broker, key, post, onError);
        }

        /// <summary>Call any capability this app was granted, exactly like a desktop app.</summary>
        public Task<T> InvokeAsync<T>(string capability, Dictionary<string, object?>? args = null)
            => _client.InvokeAsync<T>(capability, args ?? new());

        public async ValueTask DisposeAsync()
        {
            await _events.DisposeAsync();
            await _client.DisposeAsync();
        }
    }
}
