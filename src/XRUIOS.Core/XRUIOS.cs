using WISecureData;
using XRUIOS.Contracts;

namespace XRUIOS.Barebones
{
    /// <summary>
    /// The context shim. Copied system code reads <c>XRUIOS.DataPath</c> / <c>XRUIOS.PublicDataPath</c> /
    /// <c>XRUIOS.encryptionKey</c> exactly as before, but these now delegate to a bound
    /// <see cref="IXruiosContext"/>. The XRUIOS.Manager is the authority: at worker launch it binds the
    /// real paths and that worker's own key via <see cref="Bind"/> before any module runs.
    ///
    /// There is no silent fallback any more (the old CWD paths and hardcoded "Test" key are gone):
    /// reading the context before it's bound throws. A worker debugged on its own opts in with
    /// XRUIOS_DEV_STANDALONE=1, which binds a throwaway store under %LocalAppData%\XRUIOS\Dev with a
    /// random key generated for that machine.
    /// </summary>
    public static class XRUIOS
    {
        private static IXruiosContext? _ctx;

        /// <summary>Bind the authoritative context (called once by the worker host at startup).</summary>
        public static void Bind(IXruiosContext context) => _ctx = context;

        /// <summary>
        /// Bind from the environment the Manager set at launch (XRUIOS_DATA_PATH, XRUIOS_PUBLIC_PATH,
        /// XRUIOS_WORKER_KEY as base64). Called by each worker before its code runs. Without them it
        /// binds the dev store when XRUIOS_DEV_STANDALONE=1, and otherwise stays unbound.
        /// </summary>
        public static void BindFromEnvironment()
        {
            string? data = System.Environment.GetEnvironmentVariable("XRUIOS_DATA_PATH");
            string? pub = System.Environment.GetEnvironmentVariable("XRUIOS_PUBLIC_PATH");
            string? keyB64 = System.Environment.GetEnvironmentVariable("XRUIOS_WORKER_KEY");
            if (data is not null && pub is not null && !string.IsNullOrEmpty(keyB64))
            {
                _ctx = new EnvContext(data, pub, new SecureData(System.Convert.FromBase64String(keyB64)));
                return;
            }

            if (System.Environment.GetEnvironmentVariable("XRUIOS_DEV_STANDALONE") == "1")
                BindForDevelopment();
        }

        /// <summary>
        /// A disposable dev store for running one worker without the Manager. Data written here is not
        /// the user's real data and is encrypted with a per-machine random dev key, never a known one.
        /// </summary>
        public static void BindForDevelopment(string? root = null)
        {
            root ??= System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "XRUIOS", "Dev");
            System.IO.Directory.CreateDirectory(root);

            string keyFile = System.IO.Path.Combine(root, "dev.key");
            byte[] key;
            if (System.IO.File.Exists(keyFile)) key = System.IO.File.ReadAllBytes(keyFile);
            else
            {
                key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
                System.IO.File.WriteAllBytes(keyFile, key);
            }

            _ctx = new EnvContext(System.IO.Path.Combine(root, "Data"), System.IO.Path.Combine(root, "Public"), new SecureData(key));
            System.Console.WriteLine($"[XRUIOS] DEV context bound at {root} (not the user's data).");
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

        /// <summary>True once a context (the Manager's, or the explicit dev one) is bound.</summary>
        public static bool IsBound => _ctx is not null;

        public static string DataPath => Context.DataPath;

        public static string PublicDataPath => Context.PublicDataPath;

        public static SecureData encryptionKey => Context.EncryptionKey;

        private static IXruiosContext Context => _ctx ?? throw new System.InvalidOperationException(
            "XRUIOS context is not bound. Workers get it from XRUIOS.Manager at launch; to debug one alone, " +
            "set XRUIOS_DEV_STANDALONE=1 (or call XRUIOS.BindForDevelopment()).");
    }
}
