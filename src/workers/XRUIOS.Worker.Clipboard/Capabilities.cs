using System.Security.Cryptography;
using System.Text;
using EclipseProject;
using XRUIOS.Interfaces;
using XRUIOS.Barebones;
using XRUIOS.Interpreter;

namespace XRUIOS.Worker.Clipboard
{
    // Clipboard history on top of the real OS clipboard. Every read notices what the OS clipboard holds
    // now and records it; pinned items are persisted through ClipboardClass.ClipboardGroups ("Pinned")
    // so they survive a worker restart. Recent/Pin/Share are what the ClipboardHistory hole calls.
    public static class ClipboardCapabilities
    {
        private const int MaxHistory = 100;
        private const string PinnedGroup = "Pinned";
        private const string PinnedIndexKey = "index";

        private sealed record Entry(string Id, string Text, DateTimeOffset At);

        private static readonly List<Entry> History = new();          // newest first
        private static readonly Dictionary<string, Entry> Pinned = new(StringComparer.Ordinal);
        private static readonly ClipboardClass.ClipboardGroups Groups = new();
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static bool _pinnedLoaded;

        private static IClipboardInterpreter Os => XruiosInterpreter.Current.Clipboard;

        [SeaOfDirac("Clipboard.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Clipboard: " + input;

        /// <summary>"id|kind|preview|timestamp;..." - pinned first (kind "pinned"), then newest history.</summary>
        [SeaOfDirac("Recent", new[] { "limit" }, typeof(string), typeof(long))]
        public static async Task<string> Recent(long limit)
        {
            await Gate.WaitAsync();
            try
            {
                await LoadPinnedAsync();
                CaptureOs();

                int take = (int)Math.Clamp(limit, 1, MaxHistory);
                var rows = Pinned.Values.OrderByDescending(e => e.At).Select(e => Row(e, "pinned"))
                    .Concat(History.Where(e => !Pinned.ContainsKey(e.Id)).Select(e => Row(e, "text")))
                    .Take(take);
                return string.Join(';', rows);
            }
            finally { Gate.Release(); }
        }

        [SeaOfDirac("Pin", new[] { "id" }, typeof(string), typeof(string))]
        public static async Task<string> Pin(string id)
        {
            await Gate.WaitAsync();
            try
            {
                await LoadPinnedAsync();
                var e = History.FirstOrDefault(x => x.Id == id) ?? (Pinned.TryGetValue(id, out var p) ? p : null)
                        ?? throw new KeyNotFoundException($"No clipboard entry '{id}'.");
                Pinned[id] = e;
                Groups.AddToClipboard(PinnedGroup, Encoding.UTF8.GetBytes(e.Text), id);
                SavePinnedIndex();
                return "pinned";
            }
            finally { Gate.Release(); }
        }

        [SeaOfDirac("Unpin", new[] { "id" }, typeof(string), typeof(string))]
        public static async Task<string> Unpin(string id)
        {
            await Gate.WaitAsync();
            try
            {
                await LoadPinnedAsync();
                if (!Pinned.Remove(id)) throw new KeyNotFoundException($"'{id}' isn't pinned.");
                Groups.RemoveFromClipboard(PinnedGroup, id);
                SavePinnedIndex();
                return "unpinned";
            }
            finally { Gate.Release(); }
        }

        /// <summary>
        /// Send an entry somewhere. "clipboard" (alias "system") puts it back on the OS clipboard;
        /// cross-device targets land with the sync engine.
        /// </summary>
        [SeaOfDirac("Share", new[] { "id", "target" }, typeof(string), typeof(string), typeof(string))]
        public static async Task<string> Share(string id, string target)
        {
            await Gate.WaitAsync();
            try
            {
                await LoadPinnedAsync();
                var e = History.FirstOrDefault(x => x.Id == id) ?? (Pinned.TryGetValue(id, out var p) ? p : null)
                        ?? throw new KeyNotFoundException($"No clipboard entry '{id}'.");

                switch (target.ToLowerInvariant())
                {
                    case "clipboard":
                    case "system":
                        Os.SetText(e.Text);
                        Announce(Record(e.Text));
                        return "copied to clipboard";
                    default:
                        throw new NotSupportedException($"Sharing to '{target}' isn't wired yet; use 'clipboard'.");
                }
            }
            finally { Gate.Release(); }
        }

        /// <summary>Put text on the OS clipboard (and in history). Returns the entry id.</summary>
        [SeaOfDirac("Copy", new[] { "text" }, typeof(string), typeof(string))]
        public static async Task<string> Copy(string text)
        {
            await Gate.WaitAsync();
            try
            {
                Os.SetText(text);
                var e = Record(text);
                Announce(e);
                return e.Id;
            }
            finally { Gate.Release(); }
        }

        /// <summary>What's on the OS clipboard right now ("" when it holds no text).</summary>
        [SeaOfDirac("Paste", new string[0], typeof(string))]
        public static string Paste() => Os.GetText() ?? "";

        // ── internals ────────────────────────────────────────────────────────────────────────────

        private static void CaptureOs()
        {
            string? now;
            try { now = Os.GetText(); }
            catch { return; }   // clipboard locked / no session: still serve what we have
            if (!string.IsNullOrEmpty(now)) Record(now);
        }

        private static Entry Record(string text)
        {
            string id = IdFor(text);
            History.RemoveAll(e => e.Id == id);                 // re-copying moves it to the top
            var e = new Entry(id, text, DateTimeOffset.Now);
            History.Insert(0, e);
            if (History.Count > MaxHistory) History.RemoveRange(MaxHistory, History.Count - MaxHistory);
            return e;
        }

        private static void Announce(Entry e) =>
            WorkerEvents.Publish(XruiosEventNames.ClipboardChanged, EventJson.Write(new { id = e.Id, preview = Preview(e.Text) }));

        private static string Preview(string text) => text.Length > 120 ? text[..120] + "…" : text;

        /// <summary>Same text → same id, so a re-copy is one entry, not a duplicate.</summary>
        private static string IdFor(string text) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12].ToLowerInvariant();

        private static string Row(Entry e, string kind)
        {
            return $"{e.Id}|{kind}|{Wire.Field(Preview(e.Text))}|{e.At:O}";
        }

        private static async Task LoadPinnedAsync()
        {
            if (_pinnedLoaded) return;
            _pinnedLoaded = true;

            var index = await Groups.GetClipboardItem(PinnedGroup, PinnedIndexKey);
            if (index is null) return;
            foreach (var line in Encoding.UTF8.GetString(index).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('\t');
                if (parts.Length != 2) continue;
                var bytes = await Groups.GetClipboardItem(PinnedGroup, parts[0]);
                if (bytes is null) continue;
                var at = DateTimeOffset.TryParse(parts[1], out var t) ? t : DateTimeOffset.Now;
                Pinned[parts[0]] = new Entry(parts[0], Encoding.UTF8.GetString(bytes), at);
            }
        }

        private static void SavePinnedIndex()
        {
            var lines = Pinned.Values.Select(e => $"{e.Id}\t{e.At:O}");
            Groups.AddToClipboard(PinnedGroup, Encoding.UTF8.GetBytes(string.Join('\n', lines)), PinnedIndexKey);
        }
    }
}
