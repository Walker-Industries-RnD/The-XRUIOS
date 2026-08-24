using WISecureData;
using XRUIOS.Contracts;

namespace XRUIOS.Barebones
{
    /// <summary>
    /// The context shim. Copied system code reads <c>XRUIOS.DataPath</c> / <c>XRUIOS.PublicDataPath</c> /
    /// <c>XRUIOS.encryptionKey</c> exactly as before — but these now delegate to a bound
    /// <see cref="IXruiosContext"/>. The XRUIOS.Manager is the authority: at worker launch it binds the
    /// real paths and that worker's own key via <see cref="Bind"/> before any module runs.
    ///
    /// Unbound (standalone / dev), it falls back to the old CWD-relative paths and a local dev key, so
    /// nothing breaks when run outside the Manager. The old hardcoded "Test" key is now only that
    /// last-resort fallback — the real key comes from the Manager.
    /// </summary>
    public static class XRUIOS
    {
        private static IXruiosContext? _ctx;

        /// <summary>Bind the authoritative context (called once by the worker host at startup).</summary>
        public static void Bind(IXruiosContext context) => _ctx = context;

        /// <summary>
        /// Bind from the environment the Manager set at launch (XRUIOS_DATA_PATH, XRUIOS_PUBLIC_PATH,
        /// XRUIOS_WORKER_KEY as base64). Called by each worker before its code runs. If any are missing
        /// it stays unbound and the dev fallback applies.
        /// </summary>
        public static void BindFromEnvironment()
        {
            string? data = System.Environment.GetEnvironmentVariable("XRUIOS_DATA_PATH");
            string? pub = System.Environment.GetEnvironmentVariable("XRUIOS_PUBLIC_PATH");
            string? keyB64 = System.Environment.GetEnvironmentVariable("XRUIOS_WORKER_KEY");
            if (data is null || pub is null || string.IsNullOrEmpty(keyB64)) return;
            _ctx = new EnvContext(data, pub, new SecureData(System.Convert.FromBase64String(keyB64)));
        }

        private sealed class EnvContext : IXruiosContext
        {
            public string DataPath { get; }
            public string PublicDataPath { get; }
            public SecureData EncryptionKey { get; }
            public EnvContext(string data, string pub, SecureData key)
            {
                DataPath = data;
                PublicDataPath = pub;
                EncryptionKey = key;
            }
        }

        /// <summary>True once the Manager has provided a context.</summary>
        public static bool IsBound => _ctx is not null;

        public static string DataPath =>
            _ctx?.DataPath ?? System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "XRUIOS");

        public static string PublicDataPath =>
            _ctx?.PublicDataPath ?? System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "XRUIOSPublic");

        public static SecureData encryptionKey =>
            _ctx?.EncryptionKey ?? _fallbackKey;

        // Last-resort dev key when running unbound outside the Manager. Never used in the real system.
        private static readonly SecureData _fallbackKey = "Test".ToSecureData();
    }
}
