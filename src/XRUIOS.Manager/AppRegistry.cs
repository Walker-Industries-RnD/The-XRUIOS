using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace XRUIOS.Manager
{
    /// <summary>
    /// Per-app credentials the Manager mints and holds. Each app gets its own password (PSK); the
    /// Manager keeps them all and never hands one app another's. The app authenticates to the broker
    /// with its PSK under its stable appId — that id is what XRUIOS.Permission decisions key on.
    /// </summary>
    public sealed class AppRegistry
    {
        public sealed record AppCredentials(string AppId, byte[] Psk);

        private readonly ConcurrentDictionary<string, AppCredentials> _apps = new(StringComparer.Ordinal);

        /// <summary>Register an app, minting a stable id + a fresh 256-bit password.</summary>
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
}
