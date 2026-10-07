
using YuukoProtocol;

namespace XRUIOS.Barebones.Interfaces
{
    public class AlarmClass
    {
        public record Alarm
        {
            public string AlarmName;
            public DateTime AlarmTime;
            public bool IsRecurring;
            public List<DayOfWeek> RecurringDays;
            public FileRecord SoundFilePath;
            public int Volume;
            public bool IsEnabled;

            public List<string> JobIds = new(); // ADDED

            public Alarm() { }
            public Alarm(string alarmName, DateTime alarmTime, bool isRecurring, List<DayOfWeek> recurringDays, FileRecord soundFilePath, int volume, bool isEnabled)
            {
                AlarmName = alarmName;
                AlarmTime = alarmTime;
                IsRecurring = isRecurring;
                RecurringDays = recurringDays;
                SoundFilePath = soundFilePath;
                Volume = volume;
                IsEnabled = isEnabled;
            }
        }



    }
}

