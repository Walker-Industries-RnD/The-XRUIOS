using WISecureData;
using XRUIOS.Contracts;

namespace XRUIOS.Manager
{
    // The context the Manager hands a worker at launch: the paths it uses and its own encryption key.
    // A worker binds this to the static XRUIOS shim before any of its copied code runs.
    public sealed class XruiosContext : IXruiosContext
    {
        public string DataPath { get; }
        public string PublicDataPath { get; }
        public SecureData EncryptionKey { get; }

        public XruiosContext(string dataPath, string publicDataPath, SecureData key)
        {
            DataPath = dataPath;
            PublicDataPath = publicDataPath;
            EncryptionKey = key;
        }
    }
}
