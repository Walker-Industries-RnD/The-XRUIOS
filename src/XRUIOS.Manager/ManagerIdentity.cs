using System.Security.Cryptography;
using MessagePack;
using static Pariah_Cybersecurity.EasyPQC;

namespace XRUIOS.Manager
{
    /// <summary>
    /// The Manager's long-lived post-quantum signing identity. It used to be regenerated on every run;
    /// now it's created once per user, stored encrypted under a key derived from that user's master key,
    /// and reused. That's what lets the Manager act as the local certificate authority in place of
    /// WalkerWorks: a baseline it signs today still verifies tomorrow, and nothing without the master
    /// key can sign one.
    ///
    /// Workers only ever receive the public half (XRUIOS_MANAGER_PUBKEY).
    /// </summary>
    internal sealed class ManagerIdentity
    {
        public Dictionary<string, byte[]> Public { get; }
        public Dictionary<string, byte[]> Private { get; }
        public string PublicBase64 => Convert.ToBase64String(MessagePackSerializer.Serialize(Public));

        private ManagerIdentity(Dictionary<string, byte[]> pub, Dictionary<string, byte[]> priv)
        {
            Public = pub;
            Private = priv;
        }

        public static async Task<ManagerIdentity> LoadOrCreateAsync(string path, byte[] key)
        {
            byte[]? blob = SealedFile.Read(path, key);
            if (blob is not null)
            {
                var stored = MessagePackSerializer.Deserialize<Dictionary<string, Dictionary<string, byte[]>>>(blob);
                CryptographicOperations.ZeroMemory(blob);
                return new ManagerIdentity(stored["pub"], stored["priv"]);
            }

            var (pub, priv) = await Signatures.CreateKeys();
            byte[] fresh = MessagePackSerializer.Serialize(new Dictionary<string, Dictionary<string, byte[]>>
            {
                ["pub"] = pub,
                ["priv"] = priv,
            });
            SealedFile.Write(path, key, fresh);
            CryptographicOperations.ZeroMemory(fresh);
            Console.WriteLine("[Manager] Created the Manager signing identity for this user.");
            return new ManagerIdentity(pub, priv);
        }
    }
}
