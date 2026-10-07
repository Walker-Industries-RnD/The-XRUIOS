using System.Text.RegularExpressions;
using EclipseProject;
using XRUIOS.Barebones;
using XRUIOS.Interpreter;

namespace XRUIOS.Worker.Creator
{
    // Creators group files under a name: the artist of some songs, the photographer of some photos.
    // They come in TYPES ("Music", "Art", ...), and a name is unique within its type. The worker keeps
    // references to files (a file name and the folder it is in); it does not copy or open them.
    //
    // The type and the name become file names, so both are checked. The type "Media" is reserved for
    // the MediaTagger worker.
    public static class CreatorCapabilities
    {
        private static readonly Regex SafeType = new(@"^[A-Za-z0-9][A-Za-z0-9 _-]{0,39}$", RegexOptions.Compiled);
        private static readonly Regex SafeName = new(@"^[\p{L}\p{N}][\p{L}\p{N} ._'-]{0,79}$", RegexOptions.Compiled);

        [SeaOfDirac("Creator.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Creator: " + input;

        /// <summary>"name|description|files;..." for every creator of a type.</summary>
        [SeaOfDirac("ListCreators", new[] { "creatorType" }, typeof(string), typeof(string))]
        public static async Task<string> List(string creatorType)
        {
            CheckType(creatorType);
            string dir = TypeDir(creatorType);
            if (!Directory.Exists(dir)) return "";

            var rows = new List<string>();
            foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(f => f))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (name == "CreatorFavorites") continue;
                var c = await CreatorClass.CreatorFileClass.GetCreator(name, creatorType);
                if (c != null) rows.Add($"{Wire.Field(c.Name)}|{Wire.Field(c.Description)}|{c.Files?.Count ?? 0}");
            }
            return string.Join(';', rows);
        }

        [SeaOfDirac("CreateCreator", new[] { "name", "creatorType", "description" }, typeof(string), typeof(string), typeof(string), typeof(string))]
        public static async Task<string> Create(string name, string creatorType, string description = "")
        {
            CheckType(creatorType);
            CheckName(name);
            if (Exists(name, creatorType)) throw new InvalidOperationException($"'{name}' already exists as a {creatorType}.");
            await CreatorClass.CreatorFileClass.CreateCreator(name, string.IsNullOrWhiteSpace(description) ? null : description, null, new List<string>(), creatorType);
            return "created";
        }

        /// <summary>Add files to a creator. <paramref name="paths"/> are full paths separated by '|'; each file must exist.</summary>
        [SeaOfDirac("AddCreatorFiles", new[] { "name", "creatorType", "paths" }, typeof(string), typeof(string), typeof(string), typeof(string))]
        public static async Task<string> AddFiles(string name, string creatorType, string paths)
        {
            Require(name, creatorType);
            var list = Paths(paths);
            await CreatorClass.CreatorFileClass.AddFile(name, creatorType, list);
            return $"added {list.Count}";
        }

        /// <summary>"id|fileName;..." for the files of a creator.</summary>
        [SeaOfDirac("ListCreatorFiles", new[] { "name", "creatorType" }, typeof(string), typeof(string), typeof(string))]
        public static async Task<string> Files(string name, string creatorType)
        {
            Require(name, creatorType);
            var files = await CreatorClass.CreatorFileClass.GetCreatorFiles(name, creatorType) ?? new();
            return string.Join(';', files.Select(f => $"{Wire.Field(f.UUID)}|{Wire.Field(f.File)}"));
        }

        [SeaOfDirac("SetCreatorDescription", new[] { "name", "creatorType", "description" }, typeof(string), typeof(string), typeof(string), typeof(string))]
        public static async Task<string> SetDescription(string name, string creatorType, string description)
        {
            Require(name, creatorType);
            await CreatorClass.CreatorFileClass.SetDescription(name, creatorType, description ?? "");
            return "updated";
        }

        [SeaOfDirac("FavoriteCreator", new[] { "name", "creatorType" }, typeof(string), typeof(string), typeof(string))]
        public static async Task<string> Favorite(string name, string creatorType)
        {
            Require(name, creatorType);
            await CreatorClass.CreatorFavoritesClass.AddToFavorites(name, creatorType);
            return "favorited";
        }

        [SeaOfDirac("UnfavoriteCreator", new[] { "name", "creatorType" }, typeof(string), typeof(string), typeof(string))]
        public static async Task<string> Unfavorite(string name, string creatorType)
        {
            Require(name, creatorType);
            await CreatorClass.CreatorFavoritesClass.RemoveFromFavorites(name, creatorType);
            return "removed";
        }

        /// <summary>Names of the favorite creators of a type, separated by ';'.</summary>
        [SeaOfDirac("ListFavoriteCreators", new[] { "creatorType" }, typeof(string), typeof(string))]
        public static async Task<string> Favorites(string creatorType)
        {
            CheckType(creatorType);
            if (!File.Exists(Path.Combine(TypeDir(creatorType), "CreatorFavorites.json"))) return "";
            return string.Join(';', (await CreatorClass.CreatorFavoritesClass.GetFavoritePathsAsync(creatorType, onlyResolved: true))
                .Select(p => Path.GetFileNameWithoutExtension(p)));
        }

        // ---- checks ----

        private static string TypeDir(string type) => Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "Creators", type);

        private static bool Exists(string name, string type) => File.Exists(Path.Combine(TypeDir(type), name + ".json"));

        private static void Require(string name, string type)
        {
            CheckType(type);
            CheckName(name);
            if (!Exists(name, type)) throw new KeyNotFoundException($"No {type} named '{name}'.");
        }

        private static void CheckType(string type)
        {
            if (string.IsNullOrEmpty(type) || !SafeType.IsMatch(type)) throw new ArgumentException("A creator type may use letters, digits, spaces, '_' and '-' (up to 40 characters).");
            if (type.Equals("Media", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("The type 'Media' belongs to the MediaTagger worker.");
        }

        private static void CheckName(string name)
        {
            if (string.IsNullOrEmpty(name) || !SafeName.IsMatch(name) || name.Contains("..", StringComparison.Ordinal) || name == "CreatorFavorites")
                throw new ArgumentException("A creator name may use letters, digits, spaces, '.', '_', ''' and '-' (up to 80 characters).");
        }

        private static List<string> Paths(string paths)
        {
            var list = (paths ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (list.Count == 0) throw new ArgumentException("No file paths were given.");
            foreach (var p in list)
                if (!Path.IsPathFullyQualified(p) || !File.Exists(p)) throw new FileNotFoundException($"Not an existing file (give the full path): {p}");
            return list;
        }
    }
}
