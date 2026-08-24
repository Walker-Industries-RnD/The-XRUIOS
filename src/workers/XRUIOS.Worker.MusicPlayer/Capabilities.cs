using EclipseProject;

namespace XRUIOS.Worker.MusicPlayer
{
    // Placeholder capability surface. The MusicPlayer class code is compiled into this worker (isolated in
    // its own process); curated [SeaOfDirac] capabilities that expose it are added here as the
    // cross-program surface is defined.
    public static class MusicPlayerCapabilities
    {
        [SeaOfDirac("MusicPlayer.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.MusicPlayer: " + input;
    }
}
