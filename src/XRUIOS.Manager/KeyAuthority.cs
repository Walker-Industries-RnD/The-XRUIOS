using System.Security.Cryptography;
using System.Text;

namespace XRUIOS.Manager
{
    // Holds the master key once login unlocks it, and derives a distinct key for each worker. Nothing
    // the Manager guards can be decrypted while this is locked. Lock() wipes the key on logout.
    //
    // Every worker key comes from the one master via HKDF, so the Manager can reproduce any worker's
    // key on demand, but a worker only ever receives its own and can't reach another worker's store.
    public sealed class KeyAuthority
    {
        private byte[]? _masterKey;

        public bool IsUnlocked => _masterKey is not null;

        public void Unlock(byte[] masterKey)
        {
            Lock();
            _masterKey = (byte[])masterKey.Clone();
        }

        public void Lock()
        {
            if (_masterKey is not null) CryptographicOperations.ZeroMemory(_masterKey);
            _masterKey = null;
        }

        public byte[] DeriveWorkerKey(string workerName) =>
            Derive("xruios-worker:" + workerName);

        public byte[] PermissionStoreKey() =>
            Derive("xruios-permissions");

        private byte[] Derive(string label)
        {
            if (_masterKey is null) throw new InvalidOperationException("Key authority is locked. Log in first.");
            return HKDF.DeriveKey(HashAlgorithmName.SHA256, _masterKey, outputLength: 32,
                salt: null, info: Encoding.UTF8.GetBytes(label));
        }
    }
}
