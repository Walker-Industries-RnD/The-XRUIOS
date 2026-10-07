using EclipseProject;
using XRUIOS.Interpreter;
using YuukoProtocol;
using Areas = XRUIOS.Barebones.Functions.AreaManagerClass;

namespace XRUIOS.Worker.AreaManager
{
    // World points: named places in the user's space (a desk, a living room corner, a spot that follows
    // the user) that hold objects, app windows and screens. This surface manages the points themselves;
    // the objects placed inside a point are not exposed yet, only counted.
    public static class AreaManagerCapabilities
    {
        [SeaOfDirac("AreaManager.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.AreaManager: " + input;

        /// <summary>"id|name|description|userCentric|objects|apps|screens|items;..."</summary>
        [SeaOfDirac("ListWorldPoints", new string[0], typeof(string))]
        public static async Task<string> List()
        {
            var rows = new List<string>();
            foreach (var id in Areas.GetWorldPoints())
            {
                var p = await Areas.GetWorldPoint(id);
                rows.Add($"{p.Identifier}|{Wire.Field(p.PointName)}|{Wire.Field(p.PointDescription)}|{(p.UserCentric ? 1 : 0)}" +
                         $"|{p.StaticObjs?.Count ?? 0}|{p.AppObjs?.Count ?? 0}|{p.DesktopScreenObjs?.Count ?? 0}|{p.StaciaObjs?.Count ?? 0}");
            }
            return string.Join(';', rows);
        }

        /// <summary>Create a world point. <paramref name="userCentric"/> means it stays fixed relative to the user. Returns its id.</summary>
        [SeaOfDirac("AddWorldPoint", new[] { "name", "description", "userCentric" }, typeof(string), typeof(string), typeof(string), typeof(bool))]
        public static async Task<string> Add(string name, string description = "", bool userCentric = false)
        {
            CheckName(name);
            var point = new Areas.WorldPoint(
                Areas.RenderingMode.OnlyWhenVisible, Array.Empty<byte>(), name.Trim(), description ?? "",
                new FileRecord(null, ""), userCentric,
                new List<Areas.StaticObject>(), new List<Areas.App>(),
                new List<Areas.DesktopScreen>(), new List<Areas.StaciaItems>(), identifier: null);
            await Areas.AddWorldPoint(point);
            return point.Identifier;
        }

        /// <summary>
        /// Change a world point. An empty <paramref name="name"/> or <paramref name="description"/> keeps the
        /// current one; <paramref name="userCentric"/> is "", "true" or "false" ("" keeps the current value).
        /// </summary>
        [SeaOfDirac("UpdateWorldPoint", new[] { "id", "name", "description", "userCentric" },
            typeof(string), typeof(string), typeof(string), typeof(string), typeof(string))]
        public static async Task<string> Update(string id, string name = "", string description = "", string userCentric = "")
        {
            CheckId(id);
            if (!string.IsNullOrEmpty(name)) CheckName(name);
            bool? centric = userCentric switch
            {
                "" or null => null,
                "true" => true,
                "false" => false,
                _ => throw new ArgumentException("userCentric must be empty, 'true' or 'false'."),
            };

            var current = await Areas.GetWorldPoint(id);
            var changed = Areas.UpdateWorldPoint(current, new Areas.WorldPointPatch
            {
                PointName = string.IsNullOrEmpty(name) ? null : name.Trim(),
                PointDescription = string.IsNullOrEmpty(description) ? null : description,
                UserCentric = centric,
            });
            await Areas.UpdateWorldPoint(changed);
            return "updated";
        }

        [SeaOfDirac("DeleteWorldPoint", new[] { "id" }, typeof(string), typeof(string))]
        public static string Delete(string id)
        {
            CheckId(id);
            Areas.DeleteWorldPoint(id);
            return "deleted";
        }

        // The id becomes a file name, so only a GUID is accepted.
        private static void CheckId(string id)
        {
            if (!Guid.TryParseExact(id, "D", out _)) throw new ArgumentException("That is not a world point id.");
        }

        private static void CheckName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100) throw new ArgumentException("A world point needs a name of up to 100 characters.");
        }
    }
}
