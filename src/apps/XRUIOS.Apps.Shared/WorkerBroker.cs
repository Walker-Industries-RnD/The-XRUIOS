using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XRUIOS.Interfaces;

namespace XRUIOS.Apps.Shared;

/// <summary>
/// The single connection every UI app opens to the XRUIOS Manager. Wraps
/// EclipseSecureClient so a XAML page never sees the raw handshake and each
/// call site reads as one line: <c>await AppShell.Broker.InvokeAsync&lt;string&gt;("GetEvents", ...)</c>.
///
/// Credential resolution follows the exact pattern XRUIOS.SampleApp uses (which
/// is what the Manager wiki documents): env-var handoff, then a dev-creds
/// file, then self-enrollment through the Manager. Any UI app slots into the
/// same enrollment ladder without new code paths.
/// </summary>
public sealed class WorkerBroker : IAsyncDisposable
{
    private readonly EclipseSecureClient _client;
    private readonly string _brokerAddress;
    private readonly byte[] _psk;
    public string AppId { get; }

    private WorkerBroker(EclipseSecureClient client, string appId, string brokerAddress, byte[] psk)
    {
        _client = client;
        AppId = appId;
        _brokerAddress = brokerAddress;
        _psk = psk;
    }

    public static async Task<WorkerBroker> ConnectAsync(string appName)
    {
        var (appId, pskB64, brokerAddr) = ResolveCredentials(appName);
        var psk = Convert.FromBase64String(pskB64);
        var client = await EclipseSecureClient.ConnectAsync(brokerAddr, appName, psk, identity: appId);
        return new WorkerBroker(client, appId, brokerAddr, psk);
    }

    public Task<T> InvokeAsync<T>(string capability, Dictionary<string, object?>? args = null)
        => _client.InvokeAsync<T>(capability, args ?? new());

    /// <summary>
    /// Start receiving the events this app is granted (Events.&lt;Name&gt;). Runs on its own session so
    /// waiting for events never blocks <see cref="InvokeAsync{T}"/>. Dispose the result to stop.
    /// </summary>
    public XruiosEventStream Subscribe(Action<XruiosEvent> onEvent, Action<Exception>? onError = null)
        => XruiosEventStream.Start(_brokerAddress, AppId, _psk, onEvent, onError);

    public ValueTask DisposeAsync() => _client.DisposeAsync();

    // -----------------------------------------------------------------------
    // Credentials: env -> dev creds file -> self-enroll (mirrors SampleApp).
    // -----------------------------------------------------------------------

    private static (string appId, string pskB64, string brokerAddr) ResolveCredentials(string appName)
    {
        // 1) env-var handoff. The Manager sets these when it launches an app.
        var appId      = Environment.GetEnvironmentVariable("XRUIOS_APP_ID");
        var pskB64     = Environment.GetEnvironmentVariable("XRUIOS_APP_PSK");
        var brokerAddr = Environment.GetEnvironmentVariable("XRUIOS_BROKER_ADDR");
        if (appId is not null && pskB64 is not null && brokerAddr is not null)
            return (appId, pskB64, brokerAddr);

        // 2) dev creds file. Same location + KV format the SampleApp uses.
        var creds = TryDevCredsFile(appName);
        if (creds is not null) return creds.Value;

        // 3) self-enroll through the Manager (requires it to be RUNNING + unlocked).
        //    Task.Run so a sync caller from XAML doesn't deadlock on the WebView UI thread.
        var prov = Task.Run(() => XruiosEnrollment.EnrollAsync(appName)).GetAwaiter().GetResult();
        return (prov.AppId, prov.PskBase64, prov.BrokerAddress);
    }

    private static (string, string, string)? TryDevCredsFile(string appName)
    {
        try
        {
            var path = Path.Combine(
                Environment.GetEnvironmentVariable("XRUIOS_ROOT")
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XRUIOS"),
                "Public", appName + ".creds");
            if (!File.Exists(path)) return null;
            var kv = File.ReadAllLines(path)
                .Select(l => l.Split('=', 2))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
            var id = kv.GetValueOrDefault("AppId");
            var psk = kv.GetValueOrDefault("Psk");
            var broker = kv.GetValueOrDefault("Broker");
            if (id is null || psk is null || broker is null) return null;
            return (id, psk, broker);
        }
        catch { return null; }
    }
}
