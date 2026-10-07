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
        private readonly (Dictionary<string, byte[]> pub, Dictionary<string, byte[]> priv) _signKeys;

        public string IncomingDir => _incomingDir;

        /// <summary>One sealed certificate and whether it still checks out.</summary>
        public sealed record Entry(string Name, bool Valid, long Bytes, DateTime SealedAt, string? Problem);

        /// <param name="masterKey">The key certificates are encrypted under (the Manager passes one derived for the vault).</param>
        /// <param name="signPublic">The Manager's identity keys. They sign every sealed certificate, so a certificate
        /// sealed today still verifies after a restart; a fresh key per run could never be checked again.</param>
        public CertificateVault(string root, byte[] masterKey,
            Dictionary<string, byte[]> signPublic, Dictionary<string, byte[]> signPrivate)
        {
            _signKeys = (signPublic, signPrivate);
            _incomingDir = Path.Combine(root, "incoming");
            _sealedDir = Path.Combine(root, "sealed");
            _ledgerDir = Path.Combine(root, "ledger");
            Directory.CreateDirectory(_incomingDir);
            Directory.CreateDirectory(_sealedDir);
            Directory.CreateDirectory(_ledgerDir);
            _masterKey = masterKey;
        }

        /// <summary>Take the baseline snapshot of the incoming folder the first time the vault is used.</summary>
        public async Task InitializeAsync()
        {
            if (!Directory.EnumerateFileSystemEntries(_ledgerDir).Any())
                await Keeper.SnapshotDirectory(_incomingDir, _ledgerDir); // baseline
        }

        /// <summary>
        /// Seal and verify one certificate file directly, without waiting for it to appear in the incoming
        /// folder. Returns the name it is stored under. Fails if that name is already sealed.
        /// </summary>
        public async Task<string> ImportAsync(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.Length is 0 or > 64 || !name.All(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_'))
                throw new ArgumentException("A certificate file name may use letters, digits, '.', '-' and '_' (up to 64 characters).");
            if (File.Exists(Path.Combine(_sealedDir, name + ".sealed")))
                throw new InvalidOperationException($"A certificate named '{name}' is already sealed. Remove it first.");
            if (new FileInfo(path).Length > 1_000_000)
                throw new ArgumentException("A certificate file may be at most 1 MB.");

            if (!await SealAndVerifyAsync(path))
                throw new InvalidOperationException($"'{name}' could not be sealed and verified, so it was not trusted.");
            return name;
        }

        /// <summary>Every sealed certificate, re-checked now: signature valid AND decrypts.</summary>
        public async Task<List<Entry>> ListAsync()
        {
            var entries = new List<Entry>();
            foreach (var sealedFile in Directory.EnumerateFiles(_sealedDir, "*.sealed").OrderBy(f => f))
            {
                string name = Path.GetFileNameWithoutExtension(sealedFile);
                var info = new FileInfo(sealedFile);
                var (ok, problem) = await CheckAsync(name);
                entries.Add(new Entry(name, ok, info.Length, info.LastWriteTimeUtc, problem));
            }
            return entries;
        }

        /// <summary>Forget a sealed certificate.</summary>
        public bool Remove(string name)
        {
            bool removed = false;
            foreach (var ext in new[] { ".sealed", ".sig" })
            {
                string p = Path.Combine(_sealedDir, name + ext);
                if (File.Exists(p)) { File.Delete(p); removed = true; }
            }
            return removed;
        }

        private async Task<(bool ok, string? problem)> CheckAsync(string name)
        {
            string sealedPath = Path.Combine(_sealedDir, name + ".sealed"), sigPath = Path.Combine(_sealedDir, name + ".sig");
            if (!File.Exists(sigPath)) return (false, "signature file is missing");
            try
            {
                string sealedText = await File.ReadAllTextAsync(sealedPath);
                if (!await Signatures.VerifySignature(_signKeys.pub, await File.ReadAllBytesAsync(sigPath), sealedText))
                    return (false, "signature does not verify (edited, or sealed by another Manager)");
                await DataEncryptions.UnpackData(sealedText, Key());
                return (true, null);
            }
            catch (Exception ex) { return (false, "cannot be read: " + ex.Message); }
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
