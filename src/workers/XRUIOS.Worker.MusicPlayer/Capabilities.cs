using EclipseProject;
using XRUIOS.Barebones;
using XRUIOS.Interfaces;
using XRUIOS.Interpreter;
using Models = XRUIOS.Barebones.Interfaces.Songs;

namespace XRUIOS.Worker.MusicPlayer
{
    // What is playing and what is queued. The worker does not play sound: the player app (or the engine)
    // does that and TELLS the worker what it is playing, so every other app can see it and react to the
    // NowPlayingChanged event. State lives in memory and is gone when the worker restarts.
    //
    // A song is named by an id the player chooses (the Songs worker's "directoryId|file" works well)
    // plus its title, artist, album and length. The worker keeps these as given and never opens a file.
    public static class MusicPlayerCapabilities
    {
        private const int MaxQueue = 1000;
        private static readonly object Gate = new();

        [SeaOfDirac("MusicPlayer.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.MusicPlayer: " + input;

        /// <summary>"songId|title|artist|album|durationSeconds", or empty when nothing is playing.</summary>
        [SeaOfDirac("NowPlaying", new string[0], typeof(string))]
        public static string NowPlaying()
        {
            lock (Gate) return MusicPlayerClass.CurrentlyPlaying is { } s ? Row(s) : "";
        }

        [SeaOfDirac("SetNowPlaying", new[] { "songId", "title", "artist", "album", "durationSeconds" },
            typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(long))]
        public static string SetNowPlaying(string songId, string title, string artist, string album = "", long durationSeconds = 0)
        {
            var song = Make(songId, title, artist, album, durationSeconds);
            lock (Gate) MusicPlayerClass.CurrentlyPlaying = song;
            Announce(song);
            return "ok";
        }

        [SeaOfDirac("ClearNowPlaying", new string[0], typeof(string))]
        public static string ClearNowPlaying()
        {
            lock (Gate) MusicPlayerClass.CurrentlyPlaying = null;
            Announce(null);
            return "ok";
        }

        /// <summary>"index|songId|title|artist|album|durationSeconds;..." in play order.</summary>
        [SeaOfDirac("ListQueue", new string[0], typeof(string))]
        public static string ListQueue()
        {
            lock (Gate) return string.Join(';', MusicPlayerClass.Queue.Select((s, i) => $"{i}|{Row(s)}"));
        }

        [SeaOfDirac("QueueSong", new[] { "songId", "title", "artist", "album", "durationSeconds" },
            typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(long))]
        public static string QueueSong(string songId, string title, string artist, string album = "", long durationSeconds = 0)
        {
            var song = Make(songId, title, artist, album, durationSeconds);
            lock (Gate)
            {
                if (MusicPlayerClass.Queue.Count >= MaxQueue) throw new InvalidOperationException("The queue is full.");
                MusicPlayerClass.Queue.Add(song);
                return (MusicPlayerClass.Queue.Count - 1).ToString();
            }
        }

        [SeaOfDirac("RemoveFromQueue", new[] { "index" }, typeof(string), typeof(long))]
        public static string RemoveFromQueue(long index)
        {
            lock (Gate)
            {
                CheckIndex(index);
                MusicPlayerClass.Queue.RemoveAt((int)index);
                return "removed";
            }
        }

        /// <summary>Move the song at <paramref name="index"/> so it ends up at <paramref name="newIndex"/>.</summary>
        [SeaOfDirac("MoveInQueue", new[] { "index", "newIndex" }, typeof(string), typeof(long), typeof(long))]
        public static string MoveInQueue(long index, long newIndex)
        {
            lock (Gate)
            {
                CheckIndex(index);
                CheckIndex(newIndex);
                var song = MusicPlayerClass.Queue[(int)index];
                MusicPlayerClass.Queue.RemoveAt((int)index);
                MusicPlayerClass.Queue.Insert((int)newIndex, song);
                return "moved";
            }
        }

        [SeaOfDirac("ClearQueue", new string[0], typeof(string))]
        public static string ClearQueue()
        {
            lock (Gate) MusicPlayerClass.Queue = new List<Models.SongOverview>();
            return "cleared";
        }

        // ---- helpers ----

        private static string Row(Models.SongOverview s) =>
            $"{Wire.Field(s.XRUUID)}|{Wire.Field(s.SongName)}|{Wire.Field(s.TrackArtist)}|{Wire.Field(s.AlbumName)}|{(int)(s.Duration?.TotalSeconds ?? 0)}";

        private static Models.SongOverview Make(string songId, string title, string artist, string album, long seconds)
        {
            if (string.IsNullOrWhiteSpace(songId) || songId.Length > 300) throw new ArgumentException("A song needs an id of up to 300 characters.");
            if (string.IsNullOrWhiteSpace(title) || title.Length > 200) throw new ArgumentException("A song needs a title of up to 200 characters.");
            if (artist is { Length: > 200 } || album is { Length: > 200 }) throw new ArgumentException("The artist and album may be up to 200 characters.");
            if (seconds < 0 || seconds > 24 * 3600) throw new ArgumentException("The length must be between 0 and 86400 seconds.");

            return new Models.SongOverview
            {
                XRUUID = songId,
                SongName = title.Trim(),
                TrackArtist = (artist ?? "").Trim(),
                AlbumName = string.IsNullOrWhiteSpace(album) ? null : album.Trim(),
                Duration = seconds > 0 ? TimeSpan.FromSeconds(seconds) : null,
            };
        }

        private static void CheckIndex(long index)
        {
            if (index < 0 || index >= MusicPlayerClass.Queue.Count) throw new ArgumentOutOfRangeException(nameof(index), "No song at that position in the queue.");
        }

        private static void Announce(Models.SongOverview? s) =>
            WorkerEvents.Publish(XruiosEventNames.NowPlayingChanged, EventJson.Write(new NowPlayingInfo(
                s?.XRUUID ?? "", s?.SongName ?? "", s?.TrackArtist ?? "", s?.AlbumName ?? "", (int)(s?.Duration?.TotalSeconds ?? 0))));
    }
}
