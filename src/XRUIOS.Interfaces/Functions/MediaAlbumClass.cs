using YuukoProtocol;

namespace XRUIOS.Barebones.Interfaces
{
    public static class MediaAlbumClass
    {
        public record AlbumMedia
        {
            public string AlbumName;
            public string AlbumDescription;
            public bool IsFavorite;
            public Color UIColor;
            public Color UIColorAlt;
            public string CoverImageFilePath;
            public List<FileRecord> MediaPaths;
            public string Identifier;

            public AlbumMedia(string AlbumName, string AlbumDescription, bool IsFavorite, Color UIColor, Color UIColorAlt, string CoverImageFilePath, List<FileRecord> mediaPaths)
            {
                this.AlbumName = AlbumName;
                this.AlbumDescription = AlbumDescription;
                this.IsFavorite = IsFavorite;
                this.UIColor = UIColor;
                this.UIColorAlt = UIColorAlt;
                this.CoverImageFilePath = CoverImageFilePath;
                this.MediaPaths = mediaPaths;
                Identifier = Guid.NewGuid().ToString();
            }

            public AlbumMedia() { }
        }

        public sealed record AlbumMediaPatch
        {
            public string? AlbumName { get; init; }
            public string? AlbumDescription { get; init; }
            public bool? IsFavorite { get; init; }
            public Color? UIColor { get; init; }
            public Color? UIColorAlt { get; init; }
            public string? CoverImageFilePath { get; init; }
            public List<FileRecord>? MediaPaths { get; init; }
        }

        public static class AlbumMediaPatcher
        {
            public static AlbumMedia Apply(AlbumMedia original, AlbumMediaPatch patch)
            {
                return original with
                {
                    AlbumName = patch.AlbumName ?? original.AlbumName,
                    AlbumDescription = patch.AlbumDescription ?? original.AlbumDescription,
                    IsFavorite = patch.IsFavorite ?? original.IsFavorite,
                    UIColor = patch.UIColor ?? original.UIColor,
                    UIColorAlt = patch.UIColorAlt ?? original.UIColorAlt,
                    CoverImageFilePath = patch.CoverImageFilePath ?? original.CoverImageFilePath,
                    MediaPaths = patch.MediaPaths ?? original.MediaPaths,
                    Identifier = original.Identifier
                };
            }
        }

    } 
}
