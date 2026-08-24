using EclipseProject;

namespace XRUIOS.Worker.Timer
{
    // Placeholder capability surface. The Timer class code is compiled into this worker (isolated in
    // its own process); curated [SeaOfDirac] capabilities that expose it are added here as the
    // cross-program surface is defined.
    public static class TimerCapabilities
    {
        [SeaOfDirac("Timer.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Timer: " + input;
    }
}
