using EclipseProject;
using XRUIOS.Barebones;

namespace XRUIOS.Worker.Calendar
{
    // The curated, cross-program surface of the Calendar worker. Every method here is a capability an
    // app can be granted (Time.Calendar:*). WorkerOcean scans this assembly for them; the Manager's
    // permission check gates each call before it runs. Returns are plain strings so any client can read
    // them without sharing the Ical.Net types.
    public static class CalendarCapabilities
    {
        // Uniform health probe, one per worker (Calendar.Ping, Alarm.Ping, ...). The Diagnostics
        // harness invokes it to confirm this worker is up and routable without touching real data.
        [SeaOfDirac("Calendar.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Calendar: " + input;

        [SeaOfDirac("GetEvents", new[] { "day" }, typeof(string), typeof(string))]
        public static string GetEvents(string day)
        {
            var when = DateTime.Parse(day);
            var events = CalendarClass.GetEventsForDay(when);
            if (events.Count == 0) return $"No events on {day}.";
            return string.Join("; ", events.Select(e => $"{e.Start?.Value:HH:mm} {e.Summary}"));
        }

        [SeaOfDirac("AddEvent", new[] { "day", "summary" }, typeof(string), typeof(string), typeof(string))]
        public static async Task<string> AddEvent(string day, string summary)
        {
            var when = DateTime.Parse(day);
            return await CalendarClass.CreateSimpleEvent(when, summary, description: "", durationHours: 1);
        }

        // Privileged: deleting an event is a capability an app must be granted separately. The sample
        // app is given GetEvents + AddEvent but NOT this, so calling it shows the broker's refusal.
        [SeaOfDirac("DeleteEvent", new[] { "uid" }, typeof(string), typeof(string))]
        public static string DeleteEvent(string uid)
        {
            CalendarClass.DeleteEventByUid(uid);
            return $"Deleted {uid}.";
        }
    }
}
