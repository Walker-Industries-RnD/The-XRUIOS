using EclipseProject;
using XRUIOS.Barebones;
using XRUIOS.Interfaces;
using XRUIOS.Interpreter;
using TimerRecord = XRUIOS.Barebones.Interfaces.TimerManagerClass.TimerRecord;

namespace XRUIOS.Worker.Timer
{
    // Countdown timers. A timer lives in memory (it does not survive a restart of this worker) and,
    // when it finishes, the worker only ANNOUNCES it (TimerFinished). What happens then - a sound, a
    // flashing panel - is the app's business.
    public static class TimerCapabilities
    {
        private const long MaxSeconds = 7 * 24 * 3600;

        [SeaOfDirac("Timer.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Timer: " + input;

        /// <summary>Start (or restart) a named countdown. Returns the time it will finish, ISO 8601.</summary>
        [SeaOfDirac("StartTimer", new[] { "name", "seconds" }, typeof(string), typeof(string), typeof(long))]
        public static string Start(string name, long seconds)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A timer needs a name.");
            if (seconds < 1 || seconds > MaxSeconds) throw new ArgumentException($"seconds must be between 1 and {MaxSeconds}.");

            var timer = new TimerRecord(name, TimeSpan.FromSeconds(seconds),
                () => WorkerEvents.Publish(XruiosEventNames.TimerFinished, EventJson.Write(new TimerInfo(name, (int)seconds))));
            TimerManagerClass.StartTimer(timer);
            return timer.EndTime.ToString("O");
        }

        /// <summary>Add time to a running timer. Returns its new finish time.</summary>
        [SeaOfDirac("AddTimerTime", new[] { "name", "seconds" }, typeof(string), typeof(string), typeof(long))]
        public static string AddTime(string name, long seconds)
        {
            if (seconds < 1 || seconds > MaxSeconds) throw new ArgumentException($"seconds must be between 1 and {MaxSeconds}.");
            lock (TimerManagerClass.Gate)
            {
                if (!TimerManagerClass.Timers.TryGetValue(name, out var timer))
                    throw new KeyNotFoundException($"No running timer '{name}'.");
                TimerManagerClass.AddTime(name, TimeSpan.FromSeconds(seconds));
                return timer.EndTime.ToString("O");
            }
        }

        [SeaOfDirac("CancelTimer", new[] { "name" }, typeof(string), typeof(string))]
        public static string Cancel(string name)
        {
            lock (TimerManagerClass.Gate)
            {
                if (!TimerManagerClass.Timers.ContainsKey(name))
                    throw new KeyNotFoundException($"No running timer '{name}'.");
                TimerManagerClass.CancelTimer(name);
            }
            return "cancelled";
        }

        /// <summary>"name|finishesAtIso|secondsLeft;..." for every running timer.</summary>
        [SeaOfDirac("ListTimers", new string[0], typeof(string))]
        public static string List()
        {
            lock (TimerManagerClass.Gate)
            {
                var now = DateTime.Now;
                return string.Join(';', TimerManagerClass.Timers.Values.Where(t => t.IsRunning).OrderBy(t => t.EndTime)
                    .Select(t => $"{Wire.Field(t.TimerName)}|{t.EndTime:O}|{Math.Max(0, (int)(t.EndTime - now).TotalSeconds)}"));
            }
        }
    }
}
