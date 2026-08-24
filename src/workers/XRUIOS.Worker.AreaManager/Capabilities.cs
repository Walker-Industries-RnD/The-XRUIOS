using EclipseProject;

namespace XRUIOS.Worker.AreaManager
{
    // Placeholder capability surface. The AreaManager class code is compiled into this worker (isolated in
    // its own process); curated [SeaOfDirac] capabilities that expose it are added here as the
    // cross-program surface is defined.
    public static class AreaManagerCapabilities
    {
        [SeaOfDirac("AreaManager.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.AreaManager: " + input;
    }
}
