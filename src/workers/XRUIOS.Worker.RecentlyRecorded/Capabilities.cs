using EclipseProject;
using XRUIOS.Barebones;
using XRUIOS.Interpreter;
using YuukoProtocol;

namespace XRUIOS.Worker.RecentlyRecorded
{
    // The list of recordings the user made lately (a voice memo, a screen capture), newest 30. The worker
    // only keeps the list: it does not open the files.
    public static class RecentlyRecordedCapabilities
    {
        [SeaOfDirac("RecentlyRecorded.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.RecentlyRecorded: " + input;

        /// <summary>"id|path;..." newest first.</summary>
        [SeaOfDirac("RecentRecordings", new[] { "limit" }, typeof(string), typeof(long))]
        public static async Task<string> Recent(long limit = 30)
        {
            var items = await RecentlyRecordedClass.GetRecentlyRecorded();
            return string.Join(';', Enumerable.Reverse(items).Take((int)Math.Clamp(limit, 1, 30))
                .Select(f => $"{Wire.Field(f.UUID)}|{Wire.Field(f.File)}"));
        }

        /// <summary>Record that a file was just recorded. Returns its id.</summary>
        [SeaOfDirac("AddRecentRecording", new[] { "path" }, typeof(string), typeof(string))]
        public static async Task<string> Add(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A recording needs a file path.");
            var record = new FileRecord(null, path.Trim());
            await RecentlyRecordedClass.AddToRecentlyRecorded(record);
            return record.UUID;
        }

        /// <summary>Forget one recording by its id. The file itself is not touched.</summary>
        [SeaOfDirac("RemoveRecentRecording", new[] { "id" }, typeof(string), typeof(string))]
        public static async Task<string> Remove(string id)
        {
            var items = await RecentlyRecordedClass.GetRecentlyRecorded();
            var item = items.FirstOrDefault(f => f.UUID == id) ?? throw new KeyNotFoundException($"No recording with id '{id}'.");
            await RecentlyRecordedClass.DeleteSoundRecentlyRecorded(item);
            return "removed";
        }

        [SeaOfDirac("ClearRecentRecordings", new string[0], typeof(string))]
        public static async Task<string> Clear()
        {
            await RecentlyRecordedClass.ClearRecentlyRecorded();
            return "cleared";
        }
    }
}
