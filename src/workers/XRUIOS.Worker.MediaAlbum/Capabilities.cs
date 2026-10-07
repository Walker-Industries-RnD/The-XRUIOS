using EclipseProject;
using YuukoProtocol;
using AlbumMedia = XRUIOS.Barebones.Interfaces.MediaAlbumClass.AlbumMedia;
using Albums = XRUIOS.Barebones.Functions.MediaAlbumClass;

namespace XRUIOS.Worker.MediaAlbum
{
    // Photo/video albums. ListAlbums is what the MediaAlbumPicker hole parses.
    public static class MediaAlbumCapabilities
    {
        [SeaOfDirac("MediaAlbum.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.MediaAlbum: " + input;

        /// <summary>"id|title|count;..." (favorites first, then by title).</summary>
        [SeaOfDirac("ListAlbums", new string[0], typeof(string))]
        public static async Task<string> ListAlbums()
        {
            var albums = await Albums.GetMediaAlbums();
            return string.Join(";", albums
                .OrderByDescending(a => a.IsFavorite)
                .ThenBy(a => a.AlbumName, StringComparer.OrdinalIgnoreCase)
                .Select(a => $"{a.Identifier}|{Field(a.AlbumName)}|{a.MediaPaths?.Count ?? 0}"));
        }

        /// <summary>Creates an empty album and returns its id.</summary>
        [SeaOfDirac("CreateAlbum", new[] { "name", "description" }, typeof(string), typeof(string), typeof(string))]
        public static async Task<string> CreateAlbum(string name, string description = "")
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An album needs a name.");
            var album = new AlbumMedia(name.Trim(), description ?? "", false, default!, default!, "", new List<FileRecord>());
            await Albums.AddMediaAlbum(album);
            return album.Identifier;
        }

        // The wire format splits on ';' and '|', so neither may appear inside a field.
        private static string Field(string? s) => (s ?? "").Replace('|', '/').Replace(';', ',');
    }
}
