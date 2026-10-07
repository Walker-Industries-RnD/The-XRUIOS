using Hangfire;
using static XRUIOS.Barebones.Interfaces.TimerManagerClass;

namespace XRUIOS.Barebones
{
    public static class TimerManagerClass
    {
        public static Dictionary<string, TimerRecord> Timers = new Dictionary<string, TimerRecord>();

        // Hangfire fires a timer from its own thread while capabilities start and cancel timers from
        // others, so every access to Timers goes through this lock.
        public static readonly object Gate = new();

        // Start or restart timer
        public static void StartTimer(TimerRecord timer)
        {
            lock (Gate)
            {
                if (Timers.ContainsKey(timer.TimerName))
                    CancelTimer(timer.TimerName);

                timer.EndTime = DateTime.Now + timer.Duration;
                timer.IsRunning = true;
                Timers[timer.TimerName] = timer;

                ScheduleTimerJob(timer);
            }
        }

        // Add time to running timer
        public static void AddTime(string timerName, TimeSpan extra)
        {
            lock (Gate)
            {
                if (!Timers.TryGetValue(timerName, out var timer)) return;

                if (timer.IsRunning)
                {
                    timer.EndTime += extra;
                }
                else
                {
                    timer.Duration += extra;
                }

                // Cancel previous Hangfire job if it exists
                if (!string.IsNullOrEmpty(timer.HangfireJobId))
                {
                    BackgroundJob.Delete(timer.HangfireJobId);
                }

                ScheduleTimerJob(timer);
            }
        }

        // Cancel timer
        public static void CancelTimer(string timerName)
        {
            lock (Gate)
            {
                if (!Timers.TryGetValue(timerName, out var timer)) return;

                timer.IsRunning = false;

                if (!string.IsNullOrEmpty(timer.HangfireJobId))
                {
                    BackgroundJob.Delete(timer.HangfireJobId);
                }

                Timers.Remove(timerName);
            }
        }

        // Schedule Hangfire job
        private static void ScheduleTimerJob(TimerRecord timer)
        {
            var delay = timer.EndTime - DateTime.Now;
            if (delay <= TimeSpan.Zero)
            {
                FireTimer(timer.TimerName);
                return;
            }

            // Schedule a real Hangfire job and store the job ID
            timer.HangfireJobId = BackgroundJob.Schedule(() => FireTimer(timer.TimerName), delay);
        }

        // Fire timer callback
        public static void FireTimer(string timerName)
        {
            lock (Gate)
            {
                if (!Timers.TryGetValue(timerName, out var timer) || !timer.IsRunning) return;

                timer.IsRunning = false;
                timer.OnFinish?.Invoke();
                Timers.Remove(timerName);
            }
        }
    }
}
