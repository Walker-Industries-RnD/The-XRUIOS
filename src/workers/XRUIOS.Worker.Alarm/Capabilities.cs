using System.Globalization;
using EclipseProject;
using XRUIOS.Barebones;
using XRUIOS.Interfaces;
using XRUIOS.Interpreter;
using YuukoProtocol;

namespace XRUIOS.Worker.Alarm
{
    // Alarms are data + a schedule. When one goes off the worker only ANNOUNCES it (AlarmFired) - ringing,
    // flashing or buzzing a controller is the host's IAlarmInterpreter. ListAlarms/AddAlarm are what the
    // TimeSource hole calls.
    public static class AlarmCapabilities
    {
        [SeaOfDirac("Alarm.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Alarm: " + input;

        /// <summary>"name|timeIso|recurringDays|enabled;..." (recurringDays like "Mon,Wed", empty for one-time).</summary>
        [SeaOfDirac("ListAlarms", new string[0], typeof(string))]
        public static string ListAlarms() => string.Join(';', AlarmClass.Alarms.Select(a =>
            $"{Wire.Field(a.AlarmName)}|{a.AlarmTime:O}|{string.Join(',', (a.RecurringDays ?? new()).Select(d => d.ToString()[..3]))}|{(a.IsEnabled ? 1 : 0)}"));

        /// <summary>One-time alarm at <paramref name="when"/> (ISO 8601).</summary>
        [SeaOfDirac("AddAlarm", new[] { "when", "label" }, typeof(string), typeof(string), typeof(string))]
        public static Task<string> AddAlarm(string when, string label) => Add(label, when, "", "", 100);

        /// <summary>
        /// Full form: <paramref name="days"/> "Mon,Wed,Fri" makes it recurring at the time of day in
        /// <paramref name="when"/>; <paramref name="sound"/> is a file path for the host to play.
        /// </summary>
        [SeaOfDirac("AddAlarmFull", new[] { "label", "when", "days", "sound", "volume" },
            typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(long))]
        public static Task<string> AddAlarmFull(string label, string when, string days, string sound, long volume)
            => Add(label, when, days, sound, (int)Math.Clamp(volume, 0, 100));

        [SeaOfDirac("DeleteAlarm", new[] { "label" }, typeof(string), typeof(string))]
        public static async Task<string> DeleteAlarm(string label)
        {
            var matches = AlarmClass.Alarms.Where(a => a.AlarmName == label).ToList();
            if (matches.Count == 0) throw new KeyNotFoundException($"No alarm '{label}'.");
            foreach (var a in matches) await AlarmClass.DeleteAlarm(a);
            WorkerEvents.Publish(XruiosEventNames.AlarmsChanged, "{}");
            return $"deleted {matches.Count}";
        }

        /// <summary>Wired once at startup: every fire becomes an AlarmFired event.</summary>
        internal static void OnFired(Barebones.Interfaces.AlarmClass.Alarm a) =>
            WorkerEvents.Publish(XruiosEventNames.AlarmFired, EventJson.Write(new AlarmInfo(
                a.AlarmName, a.AlarmTime, a.IsRecurring, a.SoundFilePath?.File, a.Volume)));

        private static async Task<string> Add(string label, string when, string days, string sound, int volume)
        {
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("An alarm needs a label.");
            if (!DateTime.TryParse(when, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var time))
                throw new ArgumentException($"'{when}' is not a date/time.");
            if (AlarmClass.Alarms.Any(a => a.AlarmName == label && a.AlarmTime == time))
                throw new InvalidOperationException($"Alarm '{label}' at {time:g} already exists.");

            var recurring = ParseDays(days);
            var alarm = new Barebones.Interfaces.AlarmClass.Alarm(
                label, time, recurring.Count > 0, recurring,
                string.IsNullOrWhiteSpace(sound) ? null! : new FileRecord(null, sound),
                volume, isEnabled: true);

            await AlarmClass.AddAlarm(alarm);
            WorkerEvents.Publish(XruiosEventNames.AlarmsChanged, "{}");
            return $"{label} at {time:O}";
        }

        private static List<DayOfWeek> ParseDays(string days)
        {
            var list = new List<DayOfWeek>();
            foreach (var part in (days ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                // "Mon", "monday", "Tu" - any unique prefix of at least two letters ("T" is Tue or Thu).
                var matches = Enum.GetValues<DayOfWeek>()
                    .Where(d => d.ToString().StartsWith(part, StringComparison.OrdinalIgnoreCase)).ToList();
                if (part.Length < 2 || matches.Count != 1)
                    throw new ArgumentException($"'{part}' is not a day of the week.");
                if (!list.Contains(matches[0])) list.Add(matches[0]);
            }
            return list;
        }
    }
}
