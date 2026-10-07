using System.Text.RegularExpressions;
using EclipseProject;
using XRUIOS.Interpreter;
using Dirs = XRUIOS.Barebones.Songs.SongDirectoriesClass;
using Favorites = XRUIOS.Barebones.Songs.SongFavoritesClass;
using History = XRUIOS.Barebones.Songs.MusicHistoryClass;
using Lists = XRUIOS.Barebones.Songs.Playlists;
using Models = XRUIOS.Barebones.Interfaces.Songs.Playlists;
using SongGet = XRUIOS.Barebones.Songs.SongGetClass;

namespace XRUIOS.Worker.Songs
{
    // The music library: folders of audio files the user has added, favorites, what was played, and
    // playlists. A song is named by the folder it is in (a directory id) and its file name inside that
    // folder, and a listing also gives the full path so a player can open it. The worker never plays or
    // modifies a file; it only reads the folder listing and keeps lists.
    //
    // Inputs become file names and are used to build paths, so each is checked first. A file whose name
    // contains ';' or '|' is left out of listings, because those characters separate rows and fields.
    public static class SongsCapabilities
    {
        private const int MaxRows = 5000;
        private static readonly Regex SafeText = new(@"^[\p{L}\p{N}][\p{L}\p{N} ._'()&-]{0,79}$", RegexOptions.Compiled);
        private static readonly Regex SafeDirectoryId = new(@"^[A-Za-z0-9][A-Za-z0-9-]{0,63}$", RegexOptions.Compiled);
        private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
            { ".mp3", ".flac", ".m4a", ".mp4", ".ogg", ".opus", ".wav", ".aiff", ".aac" };

        [SeaOfDirac("Songs.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Songs: " + input;

        // ---- folders ----

        /// <summary>"id|name|path|resolved;..." (resolved is 1 when the folder can be found on this device).</summary>
        [SeaOfDirac("ListSongDirectories", new string[0], typeof(string))]
        public static async Task<string> ListDirectories()
        {
            var (resolved, unresolved) = await Dirs.GetSongDirectories();
            return string.Join(';', resolved.Select(d => Row(d, 1)).Concat(unresolved.Select(d => Row(d, 0))));

            static string Row(YuukoProtocol.DirectoryRecord d, int ok) => $"{Wire.Field(d.UUID)}|{Wire.Field(d.PathName)}|{Wire.Field(d.Path)}|{ok}";
        }

        /// <summary>Add a folder of music. <paramref name="path"/> must be the full path of an existing folder. Returns its id.</summary>
        [SeaOfDirac("AddSongDirectory", new[] { "path", "name" }, typeof(string), typeof(string), typeof(string))]
        public static async Task<string> AddDirectory(string path, string name)
        {
            if (string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path) || !Directory.Exists(path))
                throw new DirectoryNotFoundException("Give the full path of an existing folder.");
            CheckText(name, "folder name");

            await Dirs.AddSongDirectory(path, name);
            var (resolved, _) = await Dirs.GetSongDirectories();
            return resolved.First(d => d.Path == path).UUID;
        }

        /// <summary>Forget a music folder. The folder and its files are never deleted.</summary>
        [SeaOfDirac("RemoveSongDirectory", new[] { "directoryId" }, typeof(string), typeof(string))]
        public static async Task<string> RemoveDirectory(string directoryId)
        {
            CheckDirectoryId(directoryId);
            await Dirs.RemoveSongDirectory(directoryId, deleteDirectory: false);
            return "removed";
        }

        // ---- songs ----

        /// <summary>"directoryId|file|fullPath;..." for a folder, or for every folder when <paramref name="directoryId"/> is empty.</summary>
        [SeaOfDirac("ListSongs", new[] { "directoryId", "onlyFavorites" }, typeof(string), typeof(string), typeof(bool))]
        public static async Task<string> ListSongs(string directoryId = "", bool onlyFavorites = false) =>
            string.Join(';', await Rows(directoryId, onlyFavorites));

        /// <summary>Songs whose file name contains the text (any case), in the same row format as ListSongs.</summary>
        [SeaOfDirac("SearchSongs", new[] { "text", "onlyFavorites" }, typeof(string), typeof(string), typeof(bool))]
        public static async Task<string> Search(string text, bool onlyFavorites = false)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Give some text to search for.");
            var rows = await Rows("", onlyFavorites);
            return string.Join(';', rows.Where(r => r.Split('|')[1].Contains(text, StringComparison.OrdinalIgnoreCase)));
        }

        // ---- favorites and history ----

        [SeaOfDirac("FavoriteSong", new[] { "directoryId", "file" }, typeof(string), typeof(string), typeof(string))]
        public static async Task<string> Favorite(string directoryId, string file)
        {
            CheckSong(directoryId, file);
            await Favorites.AddToFavorites(file, directoryId);
            return "favorited";
        }

        [SeaOfDirac("UnfavoriteSong", new[] { "directoryId", "file" }, typeof(string), typeof(string), typeof(string))]
        public static async Task<string> Unfavorite(string directoryId, string file)
        {
            CheckSong(directoryId, file);
            await Favorites.RemoveFromFavorites(file, directoryId);
            return "removed";
        }

        /// <summary>The favorite songs, in the same row format as ListSongs.</summary>
        [SeaOfDirac("ListFavoriteSongs", new string[0], typeof(string))]
        public static async Task<string> ListFavorites() => string.Join(';', await Rows("", onlyFavorites: true));

        /// <summary>Note that a song was played. Playing it again moves it to the end of the history.</summary>
        [SeaOfDirac("RecordPlay", new[] { "directoryId", "file" }, typeof(string), typeof(string), typeof(string))]
        public static async Task<string> RecordPlay(string directoryId, string file)
        {
            CheckSong(directoryId, file);
            await History.AddToPlayHistory(file, directoryId);
            return "recorded";
        }

        /// <summary>Full paths of the songs played, oldest first, separated by ';'.</summary>
        [SeaOfDirac("ListPlayHistory", new string[0], typeof(string))]
        public static async Task<string> PlayHistory()
        {
            var (resolved, _) = await History.GetPlayHistory();
            return string.Join(';', resolved.Where(p => !p.Contains(';') && !p.Contains('|')).TakeLast(MaxRows));
        }

        [SeaOfDirac("ClearPlayHistory", new string[0], typeof(string))]
        public static async Task<string> ClearHistory()
        {
            await History.ClearPlayHIstory("", "");
            return "cleared";
        }

        // ---- playlists ----

        /// <summary>"id|name|description|songs|favorite;..." newest first.</summary>
        [SeaOfDirac("ListPlaylists", new string[0], typeof(string))]
        public static async Task<string> ListPlaylists() =>
            string.Join(';', (await Lists.GetAllPlaylists()).Select(p =>
                $"{p.PlaylistId}|{Wire.Field(p.PlaylistName)}|{Wire.Field(p.Description)}|{p.Songs?.Count ?? 0}|{(p.IsFavorite ? 1 : 0)}"));

        [SeaOfDirac("CreatePlaylist", new[] { "name", "description" }, typeof(string), typeof(string), typeof(string))]
        public static async Task<string> CreatePlaylist(string name, string description = "")
        {
            CheckText(name, "playlist name");
            return await Lists.CreatePlaylist(new Models.Playlist(name.Trim(), string.IsNullOrWhiteSpace(description) ? null : description));
        }

        [SeaOfDirac("DeletePlaylist", new[] { "playlistId" }, typeof(string), typeof(string))]
        public static async Task<string> DeletePlaylist(string playlistId)
        {
            CheckPlaylistId(playlistId);
            await Lists.DeletePlaylist(playlistId);
            return "deleted";
        }

        [SeaOfDirac("AddSongToPlaylist", new[] { "playlistId", "directoryId", "file" }, typeof(string), typeof(string), typeof(string), typeof(string))]
        public static async Task<string> AddToPlaylist(string playlistId, string directoryId, string file)
        {
            CheckPlaylistId(playlistId);
            CheckSong(directoryId, file);
            await Lists.AddSongToPlaylist(playlistId, file, directoryId);
            return "added";
        }

        [SeaOfDirac("RemoveSongFromPlaylist", new[] { "playlistId", "directoryId", "file" }, typeof(string), typeof(string), typeof(string), typeof(string))]
        public static async Task<string> RemoveFromPlaylist(string playlistId, string directoryId, string file)
        {
            CheckPlaylistId(playlistId);
            CheckSong(directoryId, file);
            await Lists.RemoveSongFromPlaylist(playlistId, file, directoryId);
            return "removed";
        }

        /// <summary>"directoryId|file|track;..." in playlist order.</summary>
        [SeaOfDirac("ListPlaylistSongs", new[] { "playlistId" }, typeof(string), typeof(string))]
        public static async Task<string> PlaylistSongs(string playlistId)
        {
            CheckPlaylistId(playlistId);
            var playlist = await Lists.GetPlaylist(playlistId) ?? throw new KeyNotFoundException($"No playlist '{playlistId}'.");
            return string.Join(';', playlist.Songs.OrderBy(s => s.TrackNumber)
                .Select(s => $"{Wire.Field(s.DirectoryUUID)}|{Wire.Field(s.SongFileName)}|{s.TrackNumber}"));
        }

        // ---- helpers ----

        private static async Task<List<string>> Rows(string directoryId, bool onlyFavorites)
        {
            var ids = new List<string>();
            if (string.IsNullOrEmpty(directoryId))
            {
                var (resolved, _) = await Dirs.GetSongDirectories();
                ids.AddRange(resolved.Select(d => d.UUID));
            }
            else
            {
                CheckDirectoryId(directoryId);
                ids.Add(directoryId);
            }

            var rows = new List<string>();
            foreach (var id in ids)
            {
                var (songs, _) = await SongGet.GetSongsInDirectoryAsync(id, onlyFavorites);
                foreach (var path in songs)
                {
                    if (rows.Count >= MaxRows) return rows;
                    string file = Path.GetFileName(path);
                    if (path.Contains(';') || path.Contains('|') || id.Contains(';') || id.Contains('|')) continue;
                    rows.Add($"{id}|{file}|{path}");
                }
            }
            return rows;
        }

        // A song is a folder id plus a plain file name with an audio extension, never a path.
        private static void CheckSong(string directoryId, string file)
        {
            CheckDirectoryId(directoryId);
            if (string.IsNullOrEmpty(file) || file != Path.GetFileName(file) || file.Contains("..", StringComparison.Ordinal)
                || file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !AudioExtensions.Contains(Path.GetExtension(file)))
                throw new ArgumentException("A song is a plain file name with an audio extension (no folders).");
        }

        private static void CheckDirectoryId(string id)
        {
            if (string.IsNullOrEmpty(id) || !SafeDirectoryId.IsMatch(id)) throw new ArgumentException("That is not a folder id.");
        }

        private static void CheckPlaylistId(string id)
        {
            if (!Guid.TryParseExact(id, "D", out _)) throw new ArgumentException("That is not a playlist id.");
        }

        private static void CheckText(string text, string what)
        {
            if (string.IsNullOrWhiteSpace(text) || !SafeText.IsMatch(text)) throw new ArgumentException($"The {what} may use letters, digits, spaces and . _ ' ( ) & - (up to 80 characters).");
        }
    }
}
