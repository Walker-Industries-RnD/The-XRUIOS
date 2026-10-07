using EclipseProject;
using XRUIOS.Barebones;

namespace XRUIOS.Worker.Chrono
{
    // The clock, formatted the way the user chose (12/24 hour, short/long styles; see ChronoClass).
    public static class ChronoCapabilities
    {
        [SeaOfDirac("Chrono.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Chrono: " + input;

        /// <summary>
        /// The current time, "shortTime|shortDate|longTime|longDate|timeZoneId|isoTimestamp". An empty
        /// <paramref name="tz"/> means the user's own time zone; otherwise a system or IANA zone id.
        /// </summary>
        [SeaOfDirac("Now", new[] { "tz" }, typeof(string), typeof(string))]
        public static string Now(string tz = "")
        {
            if (string.IsNullOrWhiteSpace(tz))
            {
                var (longTime, shortTime) = ChronoClass.GetTime();
                var (longDate, shortDate) = ChronoClass.GetDate();
                string zone = ChronoClass.GetTimezone("");
                var at = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zone));
                return Row(shortTime, shortDate, longTime, longDate, zone, at);
            }

            var zoneInfo = TimeZoneInfo.FindSystemTimeZoneById(tz);
            var (lt, st, ld, sd) = ChronoClass.GetTimeInTimezone(tz);
            return Row(st, sd, lt, ld, tz, TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zoneInfo));
        }

        private static string Row(string st, string sd, string lt, string ld, string zone, DateTime at) =>
            string.Join("|", st, sd, lt, ld, zone, at.ToString("yyyy-MM-ddTHH:mm:ss"));
    }
}
