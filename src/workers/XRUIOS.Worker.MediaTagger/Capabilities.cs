using System.Text.RegularExpressions;
using EclipseProject;
using XRUIOS.Interpreter;
using Tagger = XRUIOS.Barebones.Functions.MediaTagger.CreatorClass;

namespace XRUIOS.Worker.MediaTagger
{
    // Groups images and videos under a name (a tag, an album-like collection). Same idea as the Creator
    // worker, but fixed to the creator type "Media" so the two never share a store. The worker keeps
    // references to files (a file name and the folder it is in); it does not copy or open them.
    public static class MediaTaggerCapabilities
    {
        private const string MediaType = "Media";
        private static readonly Regex SafeName = new(@"^[\p{L}\p{N}][\p{L}\p{N} ._'-]{0,79}$", RegexOptions.Compiled);

        [SeaOfDirac("MediaTagger.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.MediaTagger: " + input;

        /// <summary>"name|description|files;..."</summary>
        [SeaOfDirac("ListMediaTags", new string[0], typeof(string))]
        public static async Task<string> List()
        {
            if (!Directory.Exists(TypeDir)) return "";
            var rows = new List<string>();
            foreach (var file in Directory.EnumerateFiles(TypeDir, "*.json").OrderBy(f => f))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (name == "CreatorFavorites") continue;
                var c = await Tagger.CreatorFileClass.GetCreator(name, MediaType);
                if (c != null) rows.Add($"{Wire.Field(c.Name)}|{Wire.Field(c.Description)}|{c.Files?.Count ?? 0}");
            }
            return string.Join(';', rows);
        }

        [SeaOfDirac("CreateMediaTag", new[] { "name", "description" }, typeof(string), typeof(string), typeof(string))]
        public static async Task<string> Create(string name, string description = "")
        {
            CheckName(name);
            if (Exists(name)) throw new InvalidOperationException($"The tag '{name}' already exists.");
            await Tagger.CreatorFileClass.CreateCreator(name, string.IsNullOrWhiteSpace(description) ? null : description, null, new List<string>(), MediaType);
            return "created";
        }

        /// <summary>Add images or videos to a tag. <paramref name="paths"/> are full paths separated by '|'; each file must exist.</summary>
        [SeaOfDirac("AddMediaFiles", new[] { "name", "paths" }, typeof(string), typeof(string), typeof(string))]
        public static async Task<string> AddFiles(string name, string paths)
        {
            Require(name);
            var list = (paths ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (list.Count == 0) throw new ArgumentException("No file paths were given.");
            foreach (var p in list)
                if (!Path.IsPathFullyQualified(p) || !File.Exists(p)) throw new FileNotFoundException($"Not an existing file (give the full path): {p}");

            await Tagger.CreatorFileClass.AddFile(name, MediaType, list);
            return $"added {list.Count}";
        }

        /// <summary>"id|fileName;..." for the files of a tag.</summary>
        [SeaOfDirac("ListMediaFiles", new[] { "name" }, typeof(string), typeof(string))]
        public static async Task<string> Files(string name)
        {
            Require(name);
            var files = await Tagger.CreatorFileClass.GetCreatorFiles(name, MediaType) ?? new();
            return string.Join(';', files.Select(f => $"{Wire.Field(f.UUID)}|{Wire.Field(f.File)}"));
        }

        [SeaOfDirac("FavoriteMediaTag", new[] { "name" }, typeof(string), typeof(string))]
        public static async Task<string> Favorite(string name)
        {
            Require(name);
            await Tagger.CreatorFavoritesClass.AddToFavorites(name, MediaType);
            return "favorited";
        }

        [SeaOfDirac("UnfavoriteMediaTag", new[] { "name" }, typeof(string), typeof(string))]
        public static async Task<string> Unfavorite(string name)
        {
            Require(name);
            await Tagger.CreatorFavoritesClass.RemoveFromFavorites(name, MediaType);
            return "removed";
        }

        /// <summary>Names of the favorite tags, separated by ';'.</summary>
        [SeaOfDirac("ListFavoriteMediaTags", new string[0], typeof(string))]
        public static async Task<string> Favorites()
        {
            if (!File.Exists(Path.Combine(TypeDir, "CreatorFavorites.json"))) return "";
            return string.Join(';', (await Tagger.CreatorFavoritesClass.GetFavoritePathsAsync(MediaType, onlyResolved: true))
                .Select(Wire.Field));
        }

        // ---- checks ----

        private static string TypeDir => Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "Creators", MediaType);

        private static bool Exists(string name) => File.Exists(Path.Combine(TypeDir, name + ".json"));

        private static void Require(string name)
        {
            CheckName(name);
            if (!Exists(name)) throw new KeyNotFoundException($"No tag named '{name}'.");
        }

        // The name becomes a file name.
        private static void CheckName(string name)
        {
            if (string.IsNullOrEmpty(name) || !SafeName.IsMatch(name) || name.Contains("..", StringComparison.Ordinal) || name == "CreatorFavorites")
                throw new ArgumentException("A tag name may use letters, digits, spaces, '.', '_', ''' and '-' (up to 80 characters).");
        }
    }
}
