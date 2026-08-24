
using System.Text.Json.Nodes;

namespace XRUIOS.Barebones.Interfaces;

public class Songs
{

    //Add music playlist support later










    //We have a folder with song overviews and another with songdetailed

    //We have a second folder with a huge hashset

    //Whenever we reference a song, internally we ensure that the sign is the same; if not, we update both the sign and the file

    //For images, we can get the images embedded in the file

    //However we have a third folder called "Media"; if a folder in there has the same generated UUID as the song overview file
    //we will take this image over the ones embedded (As these support gifs)

    //Also when updating the songoverview/detailed, we can simply use the data gathered from overview as input for detailed and use ATLDOTNET to fill in the holes

    public record SongOverview
    {
        // Title
        public string SongName { get; set; }
        public string? AlbumName { get; set; }

        // Artist
        public string TrackArtist { get; set; }
        public string? AlbumArtist { get; set; }

        // Media Playback
        public TimeSpan? Duration { get; set; }
        public DateTime? ReleasedOn { get; set; }
        public DateTime? LastPlayedOn { get; set; }

        // Identifiers
        public Guid? SongId { get; set; }
        public string? ISRC { get; set; }

        public string XRUUID { get; set; }

        // Other Data
        public int? TrackNumber { get; set; }
        public bool? IsFavorite { get; set; }
        public int PlayCount { get; set; }

        public SongOverview() { }


        // Constructor for easy initialization
        public SongOverview(
            string songName,
            string trackArtist,
            DateTime? releasedOn,
            string? albumName = null,
            string? albumArtist = null,
            TimeSpan? duration = null,
            DateTime? lastPlayedOn = null,
            Guid? songId = null,
            string? isrc = null,
            int? trackNumber = null,
            bool? isFavorite = null,
            int playCount = 0
        )
        {
            SongName = songName;
            TrackArtist = trackArtist;
            ReleasedOn = releasedOn;
            AlbumName = albumName;
            AlbumArtist = albumArtist;
            Duration = duration;
            LastPlayedOn = lastPlayedOn;
            SongId = songId;
            ISRC = isrc;
            TrackNumber = trackNumber;
            IsFavorite = isFavorite;
            PlayCount = playCount;
        }
    }

    public record SongDetailed
    {

        // Titles             
        public string TrackTitle { get; set; }
        public string? AlbumTitle { get; set; }
        public string? OriginalAlbumTitle { get; set; }
        public string? ContentGroupDescription { get; set; }


        // People & Organizations              
        public string TrackArtist { get; set; }
        public string? AlbumArtist { get; set; }
        public string? OriginalArtist { get; set; }
        public string? Composer { get; set; }
        public string? Conductor { get; set; }
        public string? Lyricist { get; set; }
        public string? Publisher { get; set; }
        public List<string>? InvolvedPeople { get; set; } // e.g., producers, arrangers
        public string? SeriesTitle { get; set; }


        // Count & Indexes               
        public int? TrackNumber { get; set; }
        public int? TotalTracks { get; set; }
        public int? DiscNumber { get; set; }
        public int? TotalDiscs { get; set; }
        public string? AlbumSortOrder { get; set; }
        public string? AlbumArtistSortOrder { get; set; }
        public string? ArtistSortOrder { get; set; }
        public string? TitleSortOrder { get; set; }
        public string? SeriesPartIndex { get; set; }


        // Dates               
        public DateTime? RecordingDate { get; set; }
        public int? RecordingYear { get; set; }
        public DateTime? OriginalReleaseDate { get; set; }
        public int? OriginalReleaseYear { get; set; }
        public DateTime? PublishingDate { get; set; }


        // Identifiers               
        public string? ISRC { get; set; }
        public string? CatalogNumber { get; set; }


        // Ripping & Encoding               
        public string? EncodedBy { get; set; }
        public string? Encoder { get; set; }


        // URLs               
        public string? AudioSourceUrl { get; set; }


        // Style               
        public string? Genre { get; set; }
        public int? BPM { get; set; }


        // Miscellaneous
        public string? Comment { get; set; }
        public string? Description { get; set; }
        public string? LongDescription { get; set; } // Podcast description
        public string? Language { get; set; }
        public string? Copyright { get; set; }
        public List<SongChapter>? Chapters { get; set; }
        public string? UnsynchronizedLyrics { get; set; }
        public List<LyricLine>? SynchronizedLyrics { get; set; }


        public SongDetailed() { }

        public SongDetailed(
            string trackTitle,
            string trackArtist,
            DateTime? recordingDate = null,
            string? albumTitle = null,
            string? originalAlbumTitle = null,
            string? contentGroupDescription = null,
            string? albumArtist = null,
            string? originalArtist = null,
            string? composer = null,
            string? conductor = null,
            string? lyricist = null,
            string? publisher = null,
            List<string>? involvedPeople = null,
            string? seriesTitle = null,
            int? trackNumber = null,
            int? totalTracks = null,
            int? discNumber = null,
            int? totalDiscs = null,
            string? albumSortOrder = null,
            string? albumArtistSortOrder = null,
            string? artistSortOrder = null,
            string? titleSortOrder = null,
            string? seriesPartIndex = null,
            int? recordingYear = null,
            DateTime? originalReleaseDate = null,
            int? originalReleaseYear = null,
            DateTime? publishingDate = null,
            string? isrc = null,
            string? catalogNumber = null,
            string? encodedBy = null,
            string? encoder = null,
            string? audioSourceUrl = null,
            string? genre = null,
            int? bpm = null,
            string? comment = null,
            string? description = null,
            string? longDescription = null,
            string? language = null,
            string? copyright = null,
         List<SongChapter>? chapters = null,
         string? unsynchronizedLyrics = null,
         List<LyricLine>? synchronizedLyrics = null

        )
        {
            TrackTitle = trackTitle;
            TrackArtist = trackArtist;
            RecordingDate = recordingDate;
            AlbumTitle = albumTitle;
            OriginalAlbumTitle = originalAlbumTitle;
            ContentGroupDescription = contentGroupDescription;
            AlbumArtist = albumArtist;
            OriginalArtist = originalArtist;
            Composer = composer;
            Conductor = conductor;
            Lyricist = lyricist;
            Publisher = publisher;
            InvolvedPeople = involvedPeople;
            SeriesTitle = seriesTitle;
            TrackNumber = trackNumber;
            TotalTracks = totalTracks;
            DiscNumber = discNumber;
            TotalDiscs = totalDiscs;
            AlbumSortOrder = albumSortOrder;
            AlbumArtistSortOrder = albumArtistSortOrder;
            ArtistSortOrder = artistSortOrder;
            TitleSortOrder = titleSortOrder;
            SeriesPartIndex = seriesPartIndex;
            RecordingYear = recordingYear;
            OriginalReleaseDate = originalReleaseDate;
            OriginalReleaseYear = originalReleaseYear;
            PublishingDate = publishingDate;
            ISRC = isrc;
            CatalogNumber = catalogNumber;
            EncodedBy = encodedBy;
            Encoder = encoder;
            AudioSourceUrl = audioSourceUrl;
            Genre = genre;
            BPM = bpm;
            Comment = comment;
            Description = description;
            LongDescription = longDescription;
            Language = language;
            Copyright = copyright;
            Chapters = chapters;
            UnsynchronizedLyrics = unsynchronizedLyrics;
            SynchronizedLyrics = synchronizedLyrics;
        }
    }

    public sealed record SongChapter
    {
        public TimeSpan Start { get; set; }
        public TimeSpan? End { get; set; }
        public string Title { get; set; } = string.Empty;

        public SongChapter() { }

        public SongChapter(TimeSpan start, TimeSpan? end, string title)
        {
            Start = start;
            End = end;
            Title = title ?? string.Empty;
        }
    }

    public sealed record LyricLine
    {
        public TimeSpan Timestamp { get; set; }
        public string Text { get; set; } = string.Empty;

        public LyricLine() { }

        public LyricLine(TimeSpan timestamp, string text)
        {
            Timestamp = timestamp;
            Text = text ?? string.Empty;
        }
    }





    public class SongClass
    {
   
        public enum MusicInfoStyle { overview, detailed, both };
        //U 
        public sealed class SongInfoPatch
        {
            // ───── Shared / Overview ─────
            public string? SongName { get; init; }
            public string? AlbumName { get; init; }
            public string? TrackArtist { get; init; }
            public string? AlbumArtist { get; init; }
            public TimeSpan? Duration { get; init; }
            public DateTime? ReleasedOn { get; init; }
            public DateTime? LastPlayedOn { get; init; }
            public Guid? SongId { get; init; }
            public string? ISRC { get; init; }
            public int? TrackNumber { get; init; }
            public bool? IsFavorite { get; init; }
            public int? PlayCount { get; init; }

            // ───── Detailed-only ─────
            public string? OriginalAlbumTitle { get; init; }
            public string? ContentGroupDescription { get; init; }
            public string? OriginalArtist { get; init; }
            public string? Composer { get; init; }
            public string? Conductor { get; init; }
            public string? Lyricist { get; init; }
            public string? Publisher { get; init; }
            public List<string>? InvolvedPeople { get; init; }
            public string? SeriesTitle { get; init; }

            public int? TotalTracks { get; init; }
            public int? DiscNumber { get; init; }
            public int? TotalDiscs { get; init; }

            public string? AlbumSortOrder { get; init; }
            public string? AlbumArtistSortOrder { get; init; }
            public string? ArtistSortOrder { get; init; }
            public string? TitleSortOrder { get; init; }
            public string? SeriesPartIndex { get; init; }

            public DateTime? RecordingDate { get; init; }
            public int? RecordingYear { get; init; }
            public DateTime? OriginalReleaseDate { get; init; }
            public int? OriginalReleaseYear { get; init; }
            public DateTime? PublishingDate { get; init; }

            public string? CatalogNumber { get; init; }
            public string? EncodedBy { get; init; }
            public string? Encoder { get; init; }
            public string? AudioSourceUrl { get; init; }
            public string? Genre { get; init; }
            public int? BPM { get; init; }

            public string? Comment { get; init; }
            public string? Description { get; init; }
            public string? LongDescription { get; init; }
            public string? Language { get; init; }
            public string? Copyright { get; init; }

            public List<SongChapter>? Chapters { get; init; }
            public string? UnsynchronizedLyrics { get; init; }
            public List<LyricLine>? SynchronizedLyrics { get; init; }
        }

     
        #region Utilities
        private static bool PatchTouchesOverview(SongInfoPatch p) =>
p.SongName != null ||
p.AlbumName != null ||
p.TrackArtist != null ||
p.AlbumArtist != null ||
p.Duration != null ||
p.ReleasedOn != null ||
p.LastPlayedOn != null ||
p.SongId != null ||
p.ISRC != null ||
p.TrackNumber != null ||
p.IsFavorite != null ||
p.PlayCount != null;

        private static bool PatchTouchesDetailed(SongInfoPatch p) =>
            p.OriginalAlbumTitle != null ||
            p.ContentGroupDescription != null ||
            p.OriginalArtist != null ||
            p.Composer != null ||
            p.Lyricist != null ||
            p.Publisher != null ||
            p.InvolvedPeople != null ||
            p.Chapters != null ||
            p.SynchronizedLyrics != null ||
            p.UnsynchronizedLyrics != null ||
            p.Genre != null ||
            p.BPM != null;

        [Flags]
        public enum AudioTagCapability
        {
            None = 0,

            // Core
            Title = 1 << 0,
            Album = 1 << 1,
            Artist = 1 << 2,
            AlbumArtist = 1 << 3,
            TrackNumber = 1 << 4,
            TotalTracks = 1 << 5,
            DiscNumber = 1 << 6,
            TotalDiscs = 1 << 7,

            // Sorting
            SortTitle = 1 << 8,
            SortAlbum = 1 << 9,
            SortArtist = 1 << 10,
            SortAlbumArtist = 1 << 11,

            // IDs
            ISRC = 1 << 12,
            CatalogNumber = 1 << 13,

            // Dates
            RecordingDate = 1 << 14,
            RecordingYear = 1 << 15,
            OriginalReleaseDate = 1 << 16,
            OriginalReleaseYear = 1 << 17,
            PublishingDate = 1 << 18,

            // People
            Composer = 1 << 19,
            Conductor = 1 << 20,
            Lyricist = 1 << 21,
            Publisher = 1 << 22,
            InvolvedPeople = 1 << 23,

            // Style
            Genre = 1 << 24,
            BPM = 1 << 25,
            Language = 1 << 26,

            // Text blobs
            Comment = 1 << 27,
            Description = 1 << 28,
            LongDescription = 1 << 29,

            // Lyrics / structure
            UnsyncedLyrics = 1 << 30,
            SyncedLyrics = 1 << 31,
            Chapters = 1 << 32
        }

        public static readonly Dictionary<string, AudioTagCapability> TagCapabilities =
new(StringComparer.OrdinalIgnoreCase)
{
    // ───────── MP3 (ID3v2.4) ─────────
    [".mp3"] =
    AudioTagCapability.Title |
    AudioTagCapability.Album |
    AudioTagCapability.Artist |
    AudioTagCapability.AlbumArtist |
    AudioTagCapability.TrackNumber |
    AudioTagCapability.TotalTracks |
    AudioTagCapability.DiscNumber |
    AudioTagCapability.TotalDiscs |
    AudioTagCapability.SortTitle |
    AudioTagCapability.SortAlbum |
    AudioTagCapability.SortArtist |
    AudioTagCapability.SortAlbumArtist |
    AudioTagCapability.ISRC |
    AudioTagCapability.CatalogNumber |
    AudioTagCapability.RecordingDate |
    AudioTagCapability.RecordingYear |
    AudioTagCapability.OriginalReleaseDate |
    AudioTagCapability.OriginalReleaseYear |
    AudioTagCapability.PublishingDate |
    AudioTagCapability.Composer |
    AudioTagCapability.Conductor |
    AudioTagCapability.Lyricist |
    AudioTagCapability.Publisher |
    AudioTagCapability.InvolvedPeople |
    AudioTagCapability.Genre |
    AudioTagCapability.BPM |
    AudioTagCapability.Language |
    AudioTagCapability.Comment |
    AudioTagCapability.UnsyncedLyrics |
    AudioTagCapability.SyncedLyrics |
    AudioTagCapability.Chapters,

    // ───────── FLAC (Vorbis comments) ─────────
    [".flac"] =
    AudioTagCapability.Title |
    AudioTagCapability.Album |
    AudioTagCapability.Artist |
    AudioTagCapability.AlbumArtist |
    AudioTagCapability.TrackNumber |
    AudioTagCapability.TotalTracks |
    AudioTagCapability.DiscNumber |
    AudioTagCapability.TotalDiscs |
    AudioTagCapability.SortTitle |
    AudioTagCapability.SortAlbum |
    AudioTagCapability.SortArtist |
    AudioTagCapability.SortAlbumArtist |
    AudioTagCapability.ISRC |
    AudioTagCapability.CatalogNumber |
    AudioTagCapability.RecordingDate |
    AudioTagCapability.RecordingYear |
    AudioTagCapability.OriginalReleaseDate |
    AudioTagCapability.Composer |
    AudioTagCapability.Lyricist |
    AudioTagCapability.Publisher |
    AudioTagCapability.InvolvedPeople |
    AudioTagCapability.Genre |
    AudioTagCapability.BPM |
    AudioTagCapability.Language |
    AudioTagCapability.Comment |
    AudioTagCapability.UnsyncedLyrics,

    // ───────── OGG / OPUS ─────────
    [".ogg"] =
            AudioTagCapability.Title |
    AudioTagCapability.Album |
    AudioTagCapability.Artist |
    AudioTagCapability.AlbumArtist |
    AudioTagCapability.TrackNumber |
    AudioTagCapability.TotalTracks |
    AudioTagCapability.DiscNumber |
    AudioTagCapability.TotalDiscs |
    AudioTagCapability.Composer |
    AudioTagCapability.Lyricist |
    AudioTagCapability.InvolvedPeople |
    AudioTagCapability.Genre |
    AudioTagCapability.Language |
    AudioTagCapability.Comment |
    AudioTagCapability.UnsyncedLyrics,

    [".opus"] =
    AudioTagCapability.Title |
    AudioTagCapability.Album |
    AudioTagCapability.Artist |
    AudioTagCapability.AlbumArtist |
    AudioTagCapability.TrackNumber |
    AudioTagCapability.TotalTracks |
    AudioTagCapability.DiscNumber |
    AudioTagCapability.TotalDiscs |
    AudioTagCapability.Composer |
    AudioTagCapability.Lyricist |
    AudioTagCapability.InvolvedPeople |
    AudioTagCapability.Genre |
    AudioTagCapability.Language |
    AudioTagCapability.Comment |
    AudioTagCapability.UnsyncedLyrics,

    // ───────── M4A / MP4 ─────────
    [".m4a"] =
    AudioTagCapability.Title |
    AudioTagCapability.Album |
    AudioTagCapability.Artist |
    AudioTagCapability.AlbumArtist |
    AudioTagCapability.TrackNumber |
    AudioTagCapability.DiscNumber |
    AudioTagCapability.Genre |
    AudioTagCapability.Comment |
    AudioTagCapability.Composer |
    AudioTagCapability.Language,
    [".mp4"] =
    AudioTagCapability.Title |
    AudioTagCapability.Album |
    AudioTagCapability.Artist |
    AudioTagCapability.AlbumArtist |
    AudioTagCapability.TrackNumber |
    AudioTagCapability.DiscNumber |
    AudioTagCapability.Genre |
    AudioTagCapability.Comment |
    AudioTagCapability.Composer |
    AudioTagCapability.Language
};

        private static AudioTagCapability GetRequiredCapabilities(SongInfoPatch p)
        {
            AudioTagCapability c = AudioTagCapability.None;

            if (p.SongName != null) c |= AudioTagCapability.Title;
            if (p.AlbumName != null) c |= AudioTagCapability.Album;
            if (p.TrackArtist != null) c |= AudioTagCapability.Artist;
            if (p.AlbumArtist != null) c |= AudioTagCapability.AlbumArtist;

            if (p.TrackNumber != null) c |= AudioTagCapability.TrackNumber;
            if (p.TotalTracks != null) c |= AudioTagCapability.TotalTracks;
            if (p.DiscNumber != null) c |= AudioTagCapability.DiscNumber;
            if (p.TotalDiscs != null) c |= AudioTagCapability.TotalDiscs;

            if (p.ISRC != null) c |= AudioTagCapability.ISRC;
            if (p.CatalogNumber != null) c |= AudioTagCapability.CatalogNumber;

            if (p.RecordingDate != null) c |= AudioTagCapability.RecordingDate;
            if (p.RecordingYear != null) c |= AudioTagCapability.RecordingYear;
            if (p.OriginalReleaseDate != null) c |= AudioTagCapability.OriginalReleaseDate;
            if (p.OriginalReleaseYear != null) c |= AudioTagCapability.OriginalReleaseYear;
            if (p.PublishingDate != null) c |= AudioTagCapability.PublishingDate;

            if (p.Composer != null) c |= AudioTagCapability.Composer;
            if (p.Conductor != null) c |= AudioTagCapability.Conductor;
            if (p.Lyricist != null) c |= AudioTagCapability.Lyricist;
            if (p.Publisher != null) c |= AudioTagCapability.Publisher;
            if (p.InvolvedPeople != null) c |= AudioTagCapability.InvolvedPeople;

            if (p.Genre != null) c |= AudioTagCapability.Genre;
            if (p.BPM != null) c |= AudioTagCapability.BPM;
            if (p.Language != null) c |= AudioTagCapability.Language;

            if (p.Comment != null) c |= AudioTagCapability.Comment;
            if (p.Description != null) c |= AudioTagCapability.Description;
            if (p.LongDescription != null) c |= AudioTagCapability.LongDescription;

            if (p.UnsynchronizedLyrics != null) c |= AudioTagCapability.UnsyncedLyrics;
            if (p.SynchronizedLyrics != null) c |= AudioTagCapability.SyncedLyrics;
            if (p.Chapters != null) c |= AudioTagCapability.Chapters;

            return c;
        }

        public static AudioTagCapability GetWritableCapabilities(
string audioPath,
SongInfoPatch patch)
        {
            var ext = Path.GetExtension(audioPath);

            if (!TagCapabilities.TryGetValue(ext, out var supported))
                return AudioTagCapability.None;

            var required = GetRequiredCapabilities(patch);

            // Return only what CAN be written
            return supported & required;
        }



        #endregion


        //D

    }




    public class SongGetClass
    {


     

        public enum SongSearchField
        {
            SongName,
            AlbumName,
            TrackArtist,
            AlbumArtist,
            Genre,
            ISRC,
            Comment,
            // can add more later, for now this works
        }

    

    }


    public class Playlists
    {


        // PLAYLIST RECORD - Follows your pattern!

        public record Playlist
        {
            public string PlaylistId { get; init; }
            public string PlaylistName { get; set; }
            public string? Description { get; set; }
            public DateTime CreatedAt { get; init; }
            public DateTime? LastModified { get; set; }
            public List<PlaylistEntry> Songs { get; set; }
            public string? CoverImagePath { get; set; }
            public bool IsFavorite { get; set; }
            public int PlayCount { get; set; }
            public TimeSpan? TotalDuration { get; set; }

            // Parameterless constructor for serialization
            public Playlist()
            {
                PlaylistId = Guid.NewGuid().ToString();
                PlaylistName = string.Empty;
                Songs = new List<PlaylistEntry>();
                CreatedAt = DateTime.UtcNow;
            }

            // Main constructor
            public Playlist(
                string playlistName,
                string? description = null,
                string? coverImagePath = null,
                List<PlaylistEntry>? songs = null,
                string? playlistId = null)
            {
                PlaylistId = playlistId ?? Guid.NewGuid().ToString();
                PlaylistName = playlistName;
                Description = description;
                CoverImagePath = coverImagePath;
                Songs = songs ?? new List<PlaylistEntry>();
                CreatedAt = DateTime.UtcNow;
                LastModified = DateTime.UtcNow;
                IsFavorite = false;
                PlayCount = 0;
            }
        }

        public record PlaylistEntry
        {
            public string SongFileName { get; init; }      // e.g., "song.mp3"
            public string DirectoryUUID { get; init; }      // Points to the directory
            public DateTime AddedAt { get; init; }
            public int TrackNumber { get; set; }            // Position in playlist
            public string? SongOverview { get; set; }       // Cached song name for quick display

            public PlaylistEntry() { }

            public PlaylistEntry(string songFileName, string directoryUUID, int trackNumber)
            {
                SongFileName = songFileName;
                DirectoryUUID = directoryUUID;
                AddedAt = DateTime.UtcNow;
                TrackNumber = trackNumber;
            }
        }




    }


}