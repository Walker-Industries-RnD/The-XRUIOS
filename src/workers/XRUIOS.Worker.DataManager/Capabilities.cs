using EclipseProject;
using XRUIOS.Interpreter;
using YuukoProtocol;
using Sessions = XRUIOS.Barebones.Functions.DataManagerClass.SessionClass;
using Slots = XRUIOS.Barebones.Functions.DataManagerClass.DataSlotClass;

namespace XRUIOS.Worker.DataManager
{
    // A SESSION is a piece of the user's reality made of world points (for example "Work": a desk at home
    // and a desk at a friend's). A DATA SLOT groups sessions so the user can switch between them. This
    // surface creates, lists, changes and deletes both; the world points are named by id and live in the
    // AreaManager worker, so they are not checked here.
    //
    // Every id becomes a folder name (and deleting removes the folder), so each one must be a GUID.
    public static class DataManagerCapabilities
    {
        [SeaOfDirac("DataManager.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.DataManager: " + input;

        // ---- sessions ----

        /// <summary>"id|title|description|createdIso|worldPoints;..."</summary>
        [SeaOfDirac("ListSessions", new string[0], typeof(string))]
        public static async Task<string> ListSessions()
        {
            var rows = new List<string>();
            foreach (var id in Sessions.GetSession())
            {
                var s = await Sessions.GetSession(id);
                rows.Add($"{id}|{Wire.Field(s.Title)}|{Wire.Field(s.Description)}|{s.Created:O}|{s.WorldPointIdentifiers?.Count ?? 0}");
            }
            return string.Join(';', rows);
        }

        /// <summary>Create a session. <paramref name="worldPointIds"/> is a ';'-separated list of ids. Returns the session id.</summary>
        [SeaOfDirac("AddSession", new[] { "title", "description", "worldPointIds" }, typeof(string), typeof(string), typeof(string), typeof(string))]
        public static async Task<string> AddSession(string title, string description = "", string worldPointIds = "")
        {
            CheckTitle(title);
            var session = new Sessions.Session(DateTime.UtcNow, title.Trim(), description ?? "", Ids(worldPointIds), identifier: null);
            await Sessions.AddSession(session);
            return session.Identifier;
        }

        /// <summary>Change a session's title and description. An empty value keeps the current one.</summary>
        [SeaOfDirac("UpdateSession", new[] { "id", "title", "description" }, typeof(string), typeof(string), typeof(string), typeof(string))]
        public static async Task<string> UpdateSession(string id, string title = "", string description = "")
        {
            CheckId(id);
            if (!string.IsNullOrEmpty(title)) CheckTitle(title);
            var current = await Sessions.GetSession(id);
            var changed = Sessions.UpdateSession(current, new Sessions.SessionPatch
            {
                Title = string.IsNullOrEmpty(title) ? null : title.Trim(),
                Description = string.IsNullOrEmpty(description) ? null : description,
            });
            await Sessions.AddSession(changed);
            return "updated";
        }

        /// <summary>Replace the world points of a session. An empty list clears them.</summary>
        [SeaOfDirac("SetSessionWorldPoints", new[] { "id", "worldPointIds" }, typeof(string), typeof(string), typeof(string))]
        public static async Task<string> SetSessionWorldPoints(string id, string worldPointIds)
        {
            CheckId(id);
            var current = await Sessions.GetSession(id);
            var changed = Sessions.UpdateSession(current, new Sessions.SessionPatch { WorldPointIdentifiers = Ids(worldPointIds) });
            await Sessions.AddSession(changed);
            return "updated";
        }

        [SeaOfDirac("DeleteSession", new[] { "id" }, typeof(string), typeof(string))]
        public static string DeleteSession(string id)
        {
            CheckId(id);
            Sessions.DeleteSession(id);
            return "deleted";
        }

        // ---- data slots ----

        /// <summary>"id|title|description|favorite|createdIso|sessions;..."</summary>
        [SeaOfDirac("ListDataSlots", new string[0], typeof(string))]
        public static async Task<string> ListDataSlots()
        {
            var rows = new List<string>();
            foreach (var id in Slots.GetDataSlot())
            {
                var s = await Slots.GetDataSlot(id);
                rows.Add($"{id}|{Wire.Field(s.Title)}|{Wire.Field(s.Description)}|{(s.IsFavorite ? 1 : 0)}|{s.DateAndTime:O}|{s.Sessions?.Count ?? 0}");
            }
            return string.Join(';', rows);
        }

        /// <summary>Create a data slot. <paramref name="sessionIds"/> is a ';'-separated list of session ids. Returns the slot id.</summary>
        [SeaOfDirac("AddDataSlot", new[] { "title", "description", "sessionIds" }, typeof(string), typeof(string), typeof(string), typeof(string))]
        public static async Task<string> AddDataSlot(string title, string description = "", string sessionIds = "")
        {
            CheckTitle(title);
            var slot = new Slots.DataSlot(false, null, title.Trim(), description ?? "", new FileRecord(null, ""), null, Ids(sessionIds), identifier: null);
            await Slots.AddDataSlot(slot);
            return slot.Identifier;
        }

        /// <summary>Change a data slot's title and description. An empty value keeps the current one.</summary>
        [SeaOfDirac("UpdateDataSlot", new[] { "id", "title", "description" }, typeof(string), typeof(string), typeof(string), typeof(string))]
        public static async Task<string> UpdateDataSlot(string id, string title = "", string description = "")
        {
            CheckId(id);
            if (!string.IsNullOrEmpty(title)) CheckTitle(title);
            var current = await Slots.GetDataSlot(id);
            var changed = Slots.UpdateDataSlot(current, new Slots.DataSlotPatch
            {
                Title = string.IsNullOrEmpty(title) ? null : title.Trim(),
                Description = string.IsNullOrEmpty(description) ? null : description,
            });
            await Slots.UpdateDataSlot(changed);
            return "updated";
        }

        /// <summary>Replace the sessions of a data slot. An empty list clears them.</summary>
        [SeaOfDirac("SetDataSlotSessions", new[] { "id", "sessionIds" }, typeof(string), typeof(string), typeof(string))]
        public static async Task<string> SetDataSlotSessions(string id, string sessionIds)
        {
            CheckId(id);
            var current = await Slots.GetDataSlot(id);
            var changed = Slots.UpdateDataSlot(current, new Slots.DataSlotPatch { Sessions = Ids(sessionIds) });
            await Slots.UpdateDataSlot(changed);
            return "updated";
        }

        [SeaOfDirac("SetDataSlotFavorite", new[] { "id", "favorite" }, typeof(string), typeof(string), typeof(bool))]
        public static async Task<string> SetDataSlotFavorite(string id, bool favorite)
        {
            CheckId(id);
            var current = await Slots.GetDataSlot(id);
            var changed = Slots.UpdateDataSlot(current, new Slots.DataSlotPatch { IsFavorite = favorite });
            await Slots.UpdateDataSlot(changed);
            return favorite ? "favorited" : "unfavorited";
        }

        [SeaOfDirac("DeleteDataSlot", new[] { "id" }, typeof(string), typeof(string))]
        public static string DeleteDataSlot(string id)
        {
            CheckId(id);
            Slots.DeleteDataSlot(id);
            return "deleted";
        }

        // ---- checks ----

        private static void CheckId(string id)
        {
            if (!Guid.TryParseExact(id, "D", out _)) throw new ArgumentException("That is not an id.");
        }

        private static void CheckTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title) || title.Length > 100) throw new ArgumentException("A title of up to 100 characters is needed.");
        }

        private static List<string> Ids(string list)
        {
            var ids = (list ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            foreach (var id in ids) CheckId(id);
            return ids;
        }
    }
}
