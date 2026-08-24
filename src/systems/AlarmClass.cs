using Hangfire;
using Hangfire.MemoryStorage;
using System.Collections.ObjectModel;
using static Pariah_Cybersecurity.DataHandler;
using static XRUIOS.Barebones.Interfaces.AlarmClass;
using static XRUIOS.Barebones.XRUIOS;
using YuukoProtocol;

namespace XRUIOS.Barebones
{
    public class AlarmClass
    {
     

        public static ObservableCollection<Alarm> Alarms = new();

        // C
        public static async Task AddAlarm(Alarm newAlarm)
        {
            Alarms.Add(newAlarm);

            var directoryPath = Path.Combine(DataPath, "Alarms");
            var manager = new Bindings.DirectoryManager(directoryPath);

            var alarmsFile = await JSONDataHandler.LoadJsonFile("Alarms", directoryPath);
            var alarms = (ObservableCollection<Alarm>)await JSONDataHandler.GetVariable<ObservableCollection<Alarm>>(alarmsFile, "Data", encryptionKey);

            alarms.Add(newAlarm);

            var editedJSON = await JSONDataHandler.UpdateJson<ObservableCollection<Alarm>>(alarmsFile, "Data", alarms, encryptionKey);
            await JSONDataHandler.SaveJson(editedJSON);

            AlarmScheduler.ScheduleAlarm(newAlarm);
        }

        // R
        public static async Task LoadAlarms()
        {
            var directoryPath = Path.Combine(DataPath, "Alarms");
            var manager = new Bindings.DirectoryManager(directoryPath);

            var alarmsFile = await JSONDataHandler.LoadJsonFile("Alarms", directoryPath);
            Alarms = (ObservableCollection<Alarm>)await JSONDataHandler.GetVariable<ObservableCollection<Alarm>>(alarmsFile, "Data", encryptionKey);

            AlarmScheduler.ScheduleAllAlarms();
        }

        // U
        public static async Task<Alarm?> UpdateAlarm(Alarm existingAlarm, Action<Alarm> updateAction)
        {
            if (!Alarms.Contains(existingAlarm))
                return null;

            foreach (var id in existingAlarm.JobIds)
                BackgroundJob.Delete(id);
            existingAlarm.JobIds.Clear();

            updateAction(existingAlarm);

            var directoryPath = Path.Combine(DataPath, "Alarms");
            var manager = new Bindings.DirectoryManager(directoryPath);

            var alarmsFile = await JSONDataHandler.LoadJsonFile("Alarms", directoryPath);
            var alarms = (ObservableCollection<Alarm>)await JSONDataHandler.GetVariable<ObservableCollection<Alarm>>(alarmsFile, "Data", encryptionKey);

            var index = -1;

            for (int i = 0; i < alarms.Count; i++)
            {
                if (alarms[i].AlarmName == existingAlarm.AlarmName &&
                    alarms[i].AlarmTime == existingAlarm.AlarmTime)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
                return null;

            alarms[index] = existingAlarm;


            var editedJSON = await JSONDataHandler.UpdateJson<ObservableCollection<Alarm>>(alarmsFile, "Data", alarms, encryptionKey);
            await JSONDataHandler.SaveJson(editedJSON);

            AlarmScheduler.ScheduleAlarm(existingAlarm);
            return existingAlarm;
        }

        // D
        public static async Task DeleteAlarm(Alarm alarm)
        {
            foreach (var id in alarm.JobIds)
                BackgroundJob.Delete(id);
            alarm.JobIds.Clear();

            Alarms.Remove(alarm);

            var directoryPath = Path.Combine(DataPath, "Alarms");
            var manager = new Bindings.DirectoryManager(directoryPath);

            var alarmsFile = await JSONDataHandler.LoadJsonFile("Alarms", directoryPath);
            var alarms = (ObservableCollection<Alarm>)await JSONDataHandler.GetVariable<ObservableCollection<Alarm>>(alarmsFile, "Data", encryptionKey);

            alarms.Remove(alarm);

            var editedJSON = await JSONDataHandler.UpdateJson<ObservableCollection<Alarm>>(alarmsFile, "Data", alarms, encryptionKey);
            await JSONDataHandler.SaveJson(editedJSON);
        }

        public static class AlarmScheduler
        {
            public static void ScheduleAlarm(Alarm alarm)
            {
                foreach (var id in alarm.JobIds)
                    BackgroundJob.Delete(id);
                alarm.JobIds.Clear();

                if (!alarm.IsEnabled)
                    return;

                var now = DateTime.Now;

                if (alarm.IsRecurring)
                {
                    var endWindow = now.AddDays(7);

                    for (var day = now.Date; day <= endWindow.Date; day = day.AddDays(1))
                    {
                        if (!alarm.RecurringDays.Contains(day.DayOfWeek))
                            continue;

                        var alarmTime = new DateTime(
                            day.Year, day.Month, day.Day,
                            alarm.AlarmTime.Hour, alarm.AlarmTime.Minute, alarm.AlarmTime.Second);

                        var delay = alarmTime - now;
                        string jobId = delay <= TimeSpan.Zero
                            ? BackgroundJob.Enqueue(() => FireAlarm(alarm))
                            : BackgroundJob.Schedule(() => FireAlarm(alarm), delay);

                        alarm.JobIds.Add(jobId);
                    }
                }
                else
                {
                    var delay = alarm.AlarmTime - now;
                    string jobId = delay <= TimeSpan.Zero
                        ? BackgroundJob.Enqueue(() => FireAlarm(alarm))
                        : BackgroundJob.Schedule(() => FireAlarm(alarm), delay);

                    alarm.JobIds.Add(jobId);
                }
            }

            public static void FireAlarm(Alarm alarm)
            {
                Console.WriteLine($"Alarm Triggered: {alarm.AlarmName} at {DateTime.Now}");

                if (alarm.IsRecurring)
                    ScheduleNextOccurrence(alarm);
            }

            public static void ScheduleNextOccurrence(Alarm alarm)
            {
                var now = DateTime.Now;

                for (int i = 1; i <= 7; i++)
                {
                    var nextDay = now.AddDays(i);
                    if (!alarm.RecurringDays.Contains(nextDay.DayOfWeek))
                        continue;

                    var nextAlarmTime = new DateTime(
                        nextDay.Year, nextDay.Month, nextDay.Day,
                        alarm.AlarmTime.Hour, alarm.AlarmTime.Minute, alarm.AlarmTime.Second);

                    var delay = nextAlarmTime - now;
                    var jobId = BackgroundJob.Schedule(() => FireAlarm(alarm), delay);
                    alarm.JobIds.Add(jobId);
                    break;
                }
            }

            public static void ScheduleAllAlarms()
            {
                foreach (var alarm in Alarms)
                    ScheduleAlarm(alarm);
            }
        }
    }

    public static class HangfireBootstrap
    {
        private static BackgroundJobServer? _server;

        public static void Start()
        {
            if (_server != null)
                return;

            GlobalConfiguration.Configuration
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseMemoryStorage();

            _server = new BackgroundJobServer();
            Console.WriteLine("Hangfire started.");
        }

        public static void Stop()
        {
            _server?.Dispose();
            _server = null;
        }
    }
}
