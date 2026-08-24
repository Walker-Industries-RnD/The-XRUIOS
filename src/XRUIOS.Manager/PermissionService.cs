using System.Text;
using PermissionHandler;
using WISecureData;

namespace XRUIOS.Manager
{
    /// <summary>
    /// Thin wrapper over XRUIOS.Permission's <see cref="Handler"/>. The store is encrypted JSON of
    /// {appId -> {capability: level}} gated by the Manager's MASTER key — the one password the
    /// Manager never gives to anyone. Grants use level 1; checks require level 1, so a capability
    /// that was never granted (absent) is denied.
    /// </summary>
    public sealed class PermissionService
    {
        private const string PermClass = "xruios";
        private const int GrantLevel = 1;

        private readonly Handler _handler = new();
        private readonly string _saveDir;
        private readonly byte[] _masterKey;

        public PermissionService(string saveDir, byte[] masterKey)
        {
            _saveDir = saveDir;
            _masterKey = masterKey;
            Directory.CreateDirectory(_saveDir);
        }

        public async Task EnsureStoreAsync()
        {
            if (!await _handler.PermissionFileExists(SD(PermClass), SD(_saveDir)))
                await _handler.SetupPermFile(SD(_saveDir), SD(PermClass), SD(_masterKey));
        }

        /// <summary>Grant an app permission to call a capability.</summary>
        public async Task GrantAsync(string appId, string capability)
        {
            await _handler.ChangePermission(
                SD(_saveDir), SD(PermClass), SD(capability), SD(appId), SD(_masterKey), SDInt(GrantLevel));
        }

        /// <summary>True iff the app was granted the capability.</summary>
        public async Task<bool> CheckAsync(string appId, string capability)
        {
            return await _handler.CheckPermission(
                SD(_saveDir), SD(PermClass), SD(capability), SD(appId), SD(_masterKey), requiredLevel: GrantLevel);
        }

        // SecureData zeroes the buffer it's handed, so every call must pass a FRESH array or the
        // reused key/dir would be wiped after first use (→ GCM MAC failures on the next op).
        private static SecureData SD(string s) => new SecureData(Encoding.UTF8.GetBytes(s));
        private static SecureData SD(byte[] b) => new SecureData((byte[])b.Clone());
        private static SecureData SDInt(int v) => new SecureData(BitConverter.GetBytes(v));
    }
}
