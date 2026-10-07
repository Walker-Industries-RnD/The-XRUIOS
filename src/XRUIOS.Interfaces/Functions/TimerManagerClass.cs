
namespace XRUIOS.Barebones.Interfaces
{
    public static class TimerManagerClass
    {
        public record TimerRecord
        {
            public string TimerName;
            public DateTime EndTime;
            public bool IsRunning;
            public TimeSpan Duration;
            public Action? OnFinish;
            public string? HangfireJobId; // store actual Hangfire job ID

            public TimerRecord(string timerName, TimeSpan duration, Action? onFinish = null)
            {
                TimerName = timerName;
                Duration = duration;
                EndTime = DateTime.Now + duration;
                IsRunning = false;
                OnFinish = onFinish;
            }
        }

      
    }
}
