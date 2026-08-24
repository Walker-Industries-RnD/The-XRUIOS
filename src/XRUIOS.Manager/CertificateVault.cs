using KeeperOfTomes;
using WISecureData;
using static Pariah_Cybersecurity.DataHandler;
using static Pariah_Cybersecurity.EasyPQC;

namespace XRUIOS.Manager
{
    /// <summary>
    /// The Manager's certificate vault
    ///   WalkerWorks issues a cert → the Manager ENCRYPTS + SIGNS it → VERIFIES it → and only then
    ///   trusts it ("if all is good we can continue").
    ///
    /// Roles, split the way each library is actually good at:
    ///   • KeeperOfTomes  — WATCHES the incoming folder and tells us the moment a cert is added
    ///                      (xxHash64 snapshot ledger; it detects, it does not encrypt).
    ///   • Pariah         — ENCRYPTS the cert bytes with the Manager's master key (PackData) and
    ///                      SIGNS the ciphertext with a post-quantum keypair (EasyPQC.Signatures).
    ///   • The Manager    — VERIFIES both (signature valid + decrypts back to the original) before
    ///                      the cert is considered trusted.
    ///
    /// WalkerWorks doesn't exist yet, so "issuance" here is a file dropped into <c>incoming/</c>.
    /// </summary>
    public sealed class CertificateVault
    {
        private readonly string _incomingDir;   // WalkerWorks drops plaintext certs here
        private readonly string _sealedDir;      // encrypted + signed output
        private readonly string _ledgerDir;      // KeeperOfTomes snapshot ledger
        private readonly byte[] _masterKey;
        private (Dictionary<string, byte[]> pub, Dictionary<string, byte[]> priv) _signKeys;

        public string IncomingDir => _incomingDir;

        public CertificateVault(string root, byte[] masterKey)
        {
            _incomingDir = Path.Combine(root, "incoming");
            _sealedDir = Path.Combine(root, "sealed");
            _ledgerDir = Path.Combine(root, "ledger");
            Directory.CreateDirectory(_incomingDir);
            Directory.CreateDirectory(_sealedDir);
            Directory.CreateDirectory(_ledgerDir);
            _masterKey = masterKey;
        }

        /// <summary>Generate the vault's signing keypair and take a baseline snapshot of the folder.</summary>
        public async Task InitializeAsync()
        {
            _signKeys = await Signatures.CreateKeys();
            await Keeper.SnapshotDirectory(_incomingDir, _ledgerDir); // baseline (empty)
        }

        /// <summary>
        /// Ask KeeperOfTomes what changed, then seal + verify every newly added/updated cert.
        /// Returns the names of certs that are now trusted.
        /// </summary>
        public async Task<List<string>> ScanAndSealAsync()
        {
            var changes = await Keeper.SnapshotDirectory(_incomingDir, _ledgerDir);
            var incoming = (changes.AddedFiles ?? new()).Concat(changes.UpdatedFiles ?? new()).Distinct();

            var trusted = new List<string>();
            foreach (var path in incoming)
                if (await SealAndVerifyAsync(path))
                    trusted.Add(Path.GetFileName(path));
            return trusted;
        }

        private async Task<bool> SealAndVerifyAsync(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            string content = await File.ReadAllTextAsync(path);

            // 1. Encrypt with the Manager's master key.
            string sealedText = await DataEncryptions.PackData<string>(content, Key());

            // 2. Post-quantum sign the ciphertext.
            byte[] signature = await Signatures.CreateSignature(_signKeys.priv, sealedText);

            // 3. Persist the sealed cert + its signature.
            await File.WriteAllTextAsync(Path.Combine(_sealedDir, name + ".sealed"), sealedText);
            await File.WriteAllBytesAsync(Path.Combine(_sealedDir, name + ".sig"), signature);

            // 4. Verify: signature checks out AND it decrypts back to the original.
            bool signatureOk = await Signatures.VerifySignature(_signKeys.pub, signature, sealedText);
            string roundTrip = (string)await DataEncryptions.UnpackData(sealedText, Key());
            bool ok = signatureOk && roundTrip == content;

            Console.WriteLine(ok
                ? $"    [Vault] {name}: encrypted + signed + VERIFIED → trusted."
                : $"    [Vault] {name}: verification FAILED → rejected.");
            return ok;
        }

        // Fresh SecureData each time — it zeroes the buffer it's given (see PermissionService).
        private SecureData Key() => new SecureData((byte[])_masterKey.Clone());
    }
}
