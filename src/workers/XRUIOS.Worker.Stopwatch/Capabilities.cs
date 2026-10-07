using EclipseProject;
using XRUIOS.Barebones;

namespace XRUIOS.Worker.Stopwatch
{
    // Stopwatches live in this worker's memory: start one, take laps, read it, stop it. The id
    // StopwatchStart returns is what every other call takes.
    public static class StopwatchCapabilities
    {
        // StopwatchClass keeps a plain Dictionary; the broker can have more than one call in flight.
        private static readonly object Gate = new();

        [SeaOfDirac("Stopwatch.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Stopwatch: " + input;

        [SeaOfDirac("StopwatchStart", new string[0], typeof(string))]
        public static string StopwatchStart()
        {
            lock (Gate) return StopwatchClass.CreateStopwatch();
        }

        /// <summary>Elapsed time as "hh:mm:ss.fff".</summary>
        [SeaOfDirac("StopwatchElapsed", new[] { "id" }, typeof(string), typeof(string))]
        public static string StopwatchElapsed(string id)
        {
            lock (Gate) return Format(StopwatchClass.GetTimeElapsed(id));
        }

        /// <summary>Records a lap. Returns "lapNumber|secondsElapsed".</summary>
        [SeaOfDirac("StopwatchLap", new[] { "id" }, typeof(string), typeof(string))]
        public static string StopwatchLap(string id)
        {
            lock (Gate)
            {
                Known(id);
                var lap = StopwatchClass.CreateLap(id);
                return $"{lap.LapCount + 1}|{lap.SecondsElapsed}";
            }
        }

        /// <summary>Stops and removes it. Returns its final time, then its laps: "hh:mm:ss.fff;1|12;2|30".</summary>
        [SeaOfDirac("StopwatchStop", new[] { "id" }, typeof(string), typeof(string))]
        public static string StopwatchStop(string id)
        {
            lock (Gate)
            {
                string total = Format(StopwatchClass.GetTimeElapsed(id));
                var laps = StopwatchClass.DestroyStopwatch(id);
                return string.Join(";", new[] { total }.Concat(laps.Select(l => $"{l.LapCount + 1}|{l.SecondsElapsed}")));
            }
        }

        private static void Known(string id)
        {
            if (!StopwatchClass.StopWatches.ContainsKey(id))
                throw new InvalidOperationException("A Stopwatch with this ID does not exist.");
        }

        private static string Format(TimeSpan t) => t.ToString(@"hh\:mm\:ss\.fff");
    }
}
