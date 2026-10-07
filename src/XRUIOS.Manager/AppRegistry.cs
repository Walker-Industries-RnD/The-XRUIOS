using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XRUIOS.Manager
{
    /// <summary>
    /// What an app is allowed to be: its name (the appId is "app:" + name), the executable that may
    /// enroll as it, the capabilities it may call and the events it may receive. Built-in apps ship one
    /// in code (<see cref="AppCatalog"/>); third-party apps are installed from a JSON file of this shape
    /// with <c>XRUIOS.Manager install-app</c>.
    /// </summary>
    public sealed record AppManifest(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("exe")] string? Exe,
        [property: JsonPropertyName("capabilities")] string[] Capabilities,
        [property: JsonPropertyName("events")] string[]? Events)
    {
        public string AppId => "app:" + Name;
        public string[] EventList => Events ?? Array.Empty<string>();
    }

    /// <summary>
    /// Per-app credentials for THIS run. Each app gets its own fresh 256-bit password (PSK) every time
    /// the Manager starts; it lives only in Manager memory and reaches the app through attested
    /// enrollment or env handoff, so there is no app secret at rest to steal. The broker authenticates
    /// an app with its PSK under its stable appId, which is what permission decisions key on.
    /// </summary>
    public sealed class AppRegistry
    {
        public sealed record AppCredentials(string AppId, byte[] Psk);

        private readonly ConcurrentDictionary<string, AppCredentials> _apps = new(StringComparer.Ordinal);

        /// <summary>Register an app for this run, minting a stable id + a fresh 256-bit password.</summary>
        public AppCredentials Register(string appName)
        {
            string appId = "app:" + appName;
            var cred = new AppCredentials(appId, RandomNumberGenerator.GetBytes(32));
            _apps[appId] = cred;
            return cred;
        }

        /// <summary>The broker's PSK resolver: map an authenticated appId to its password, or null to reject.</summary>
        public byte[]? ResolvePsk(string appId) =>
            _apps.TryGetValue(appId, out var cred) ? cred.Psk : null;
    }

    /// <summary>
    /// The persistent, encrypted list of installed (third-party) apps plus what each app was granted
    /// last time, so a capability removed from a manifest is actually revoked. Stored with
    /// <see cref="SealedFile"/> under a key derived from the user's master key: a process that can
    /// write the user's files still can't point an app id at a different executable.
    /// </summary>
    public sealed class InstalledAppStore
    {
        public sealed class State
        {
            [JsonPropertyName("apps")] public List<AppManifest> Apps { get; set; } = new();
            [JsonPropertyName("granted")] public Dictionary<string, string[]> Granted { get; set; } = new();
        }

        private readonly string _path;
        private readonly byte[] _key;

        public InstalledAppStore(string path, byte[] key)
        {
            _path = path;
            _key = key;
        }

        public State Load()
        {
            byte[]? json = SealedFile.Read(_path, _key);
            return json is null ? new State() : JsonSerializer.Deserialize<State>(json) ?? new State();
        }

        public void Save(State state) => SealedFile.Write(_path, _key, JsonSerializer.SerializeToUtf8Bytes(state));
    }
}
