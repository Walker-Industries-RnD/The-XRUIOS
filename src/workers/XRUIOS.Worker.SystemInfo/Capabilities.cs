using EclipseProject;

namespace XRUIOS.Worker.SystemInfo
{
    // Placeholder capability surface. The SystemInfo class code is compiled into this worker (isolated in
    // its own process); curated [SeaOfDirac] capabilities that expose it are added here as the
    // cross-program surface is defined.
    public static class SystemInfoCapabilities
    {
        [SeaOfDirac("SystemInfo.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.SystemInfo: " + input;
    }
}
