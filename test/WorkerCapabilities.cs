using EclipseProject;

namespace test
{
    // The worker's exposed capabilities.
    //
    // Every [SeaOfDirac] method becomes callable over Eclipse's encrypted channel once the Manager's
    // permission gate approves the caller. WorkerOcean scans THIS assembly for these methods, so they
    // must live in the worker exe itself (not a referenced library).
    //
    // Attribute shape: [SeaOfDirac(name, parameterNames, returnType, params parameterTypes)].
    // Methods may be static (shown here) or instance, and may be sync or async (Task / Task<T>).
    public static class WorkerCapabilities
    {
        // SINGULAR worker: one body, the SAME on every OS — because the functions are the same. You
        // ship this single build everywhere and there is no Windows/Linux split to maintain. If just
        // one line ever needs to differ, branch inline with OperatingSystem.IsWindows() and you STILL
        // keep one worker.
        [SeaOfDirac("DoWork", new[] { "input" }, typeof(string), typeof(string))]
        public static string DoWork(string input)
        {
            Console.WriteLine($"[test] DoWork({input})");
            return $"Handled '{input}' — same code path on Windows, Linux, and macOS.";
        }
    }
}
