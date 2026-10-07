using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace XRUIOS.Interfaces
{
    /// <summary>
    /// Append-only, tamper-evident log. Each line is one JSON entry carrying the previous entry's MAC
    /// and its own HMAC-SHA256 over (its fields + that previous MAC), so editing, reordering or deleting
    /// any line breaks the chain from that point on and <see cref="Verify"/> names the first bad line.
    ///
    /// Workers log every call they serve; the Manager logs every broker decision (ALLOW and DENY).
    /// Nothing in XRUIOS exposes a way to rewrite a log - the Manager reads and verifies them
    /// (<c>XRUIOS.Manager audit</c>). The keys come from the master key, so the guarantee is
    /// tamper-EVIDENCE against anything that doesn't hold it, not a write-once disk.
    /// </summary>
    public sealed class AuditTrail
    {
        public sealed record Entry(long Seq, DateTimeOffset At, string Actor, string Action, string Target,
            bool Ok, string? Detail, string Prev, string Mac);

        public sealed record VerifyResult(bool Ok, long Entries, long FirstBadSeq, string? Problem);

        private const string Genesis = "genesis";
        private readonly string _path;
        private readonly byte[] _key;
        private readonly object _gate = new();

        public string Path => _path;

        public AuditTrail(string path, byte[] key)
        {
            _path = path;
            _key = (byte[])key.Clone();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        }

        /// <summary>A log key bound to one purpose, derived from a worker or Manager key.</summary>
        public static byte[] KeyFrom(byte[] baseKey) =>
            HKDF.DeriveKey(HashAlgorithmName.SHA256, baseKey, 32, salt: null, info: Encoding.UTF8.GetBytes("xruios-audit"));

        /// <summary>
        /// The audit trail for this worker process, or null when it wasn't launched by the Manager
        /// (no key/path to sign with - dev standalone runs aren't audited).
        /// </summary>
        public static AuditTrail? ForWorker(string serverName)
        {
            string? data = Environment.GetEnvironmentVariable("XRUIOS_DATA_PATH");
            string? keyB64 = Environment.GetEnvironmentVariable("XRUIOS_WORKER_KEY");
            if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(keyB64)) return null;
            byte[] workerKey = Convert.FromBase64String(keyB64);
            try { return new AuditTrail(WorkerLogPath(data, serverName), KeyFrom(workerKey)); }
            finally { CryptographicOperations.ZeroMemory(workerKey); }
        }

        public static string WorkerLogPath(string dataPath, string serverName) =>
            System.IO.Path.Combine(dataPath, "Audit", serverName + ".log");

        public void Append(string actor, string action, string target, bool ok, string? detail = null)
        {
            // More than one process can write the same log (the running Manager and a CLI verb such as
            // `baseline`), so the chain head is re-read from disk under an exclusive write handle every
            // time instead of being remembered in memory. Readers can still open the file meanwhile.
            lock (_gate)
            {
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    try
                    {
                        using var fs = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                        var (lastSeq, prev) = ReadHead(fs);
                        long seq = lastSeq + 1;
                        var at = DateTimeOffset.UtcNow;
                        string mac = Mac(_key, seq, at, actor, action, target, ok, detail, prev);
                        var entry = new Entry(seq, at, actor, action, target, ok, detail, prev, mac);

                        byte[] line = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry) + "\n");
                        fs.Seek(0, SeekOrigin.End);
                        fs.Write(line);
                        return;
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(25); // another process holds the write handle
                    }
                }
                // Never fail a call because the log stayed busy; the gap is visible as missing calls.
            }
        }

        /// <summary>
        /// The newest entry's seq and MAC. The Manager records this for each worker log in its own log
        /// (an "anchor"), so cutting entries off the end of a worker log is caught by `audit` too.
        /// </summary>
        public (long Seq, string Mac) Head()
        {
            if (!File.Exists(_path)) return (0, Genesis);
            using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return ReadHead(fs);
        }

        // The last entry's seq and MAC, read from the tail of the file (lines are well under 64 KB).
        private static (long seq, string mac) ReadHead(FileStream fs)
        {
            if (fs.Length == 0) return (0, Genesis);
            long start = Math.Max(0, fs.Length - 65536);
            fs.Seek(start, SeekOrigin.Begin);
            byte[] buf = new byte[fs.Length - start];
            fs.ReadExactly(buf);
            string tail = Encoding.UTF8.GetString(buf);
            string? last = tail.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            var e = last is null ? null : JsonSerializer.Deserialize<Entry>(last);
            return e is null ? (0, Genesis) : (e.Seq, e.Mac);
        }

        public static IEnumerable<Entry> ReadEntries(string path)
        {
            if (!File.Exists(path)) yield break;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var r = new StreamReader(fs);
            string? line;
            while ((line = r.ReadLine()) != null)
            {
                if (line.Length == 0) continue;
                Entry? e = null;
                try { e = JsonSerializer.Deserialize<Entry>(line); } catch { }
                yield return e ?? new Entry(-1, default, "", "", "", false, "unreadable line", "", "");
            }
        }

        /// <summary>Walk the whole chain. Ok only if every MAC verifies and every link points at the previous one.</summary>
        public static VerifyResult Verify(string path, byte[] key)
        {
            string prev = Genesis;
            long expected = 1, count = 0;
            foreach (var e in ReadEntries(path))
            {
                count++;
                if (e.Seq != expected)
                    return new VerifyResult(false, count, expected, $"sequence jumps to {e.Seq} (entries missing or reordered)");
                if (e.Prev != prev)
                    return new VerifyResult(false, count, e.Seq, "chain link broken (an earlier entry was changed or removed)");
                string mac = Mac(key, e.Seq, e.At, e.Actor, e.Action, e.Target, e.Ok, e.Detail, e.Prev);
                if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(mac), SafeHex(e.Mac)))
                    return new VerifyResult(false, count, e.Seq, "entry was modified (MAC mismatch)");
                prev = e.Mac;
                expected++;
            }
            return new VerifyResult(true, count, -1, null);
        }

        private static byte[] SafeHex(string s)
        {
            try { return Convert.FromHexString(s); } catch { return Array.Empty<byte>(); }
        }

        private static string Mac(byte[] key, long seq, DateTimeOffset at, string actor, string action,
            string target, bool ok, string? detail, string prev)
        {
            string body = string.Join("\n", seq, at.ToUnixTimeMilliseconds(), actor, action, target, ok ? "1" : "0", detail ?? "", prev);
            return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(body)));
        }
    }
}
