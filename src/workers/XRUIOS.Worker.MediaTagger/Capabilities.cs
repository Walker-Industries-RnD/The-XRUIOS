using EclipseProject;

namespace XRUIOS.Worker.MediaTagger
{
    // Placeholder capability surface. The MediaTagger class code is compiled into this worker (isolated in
    // its own process); curated [SeaOfDirac] capabilities that expose it are added here as the
    // cross-program surface is defined.
    public static class MediaTaggerCapabilities
    {
        [SeaOfDirac("MediaTagger.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.MediaTagger: " + input;
    }
}
