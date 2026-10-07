using System.Text.Json;
using EclipseProject;
using Ical.Net.CalendarComponents;
using XRUIOS.Barebones;

namespace XRUIOS.Worker.Calendar
{
    // The curated, cross-program surface of the Calendar worker. Every method here is a capability an
    // app can be granted (Time.Calendar:*). WorkerOcean scans this assembly for them; the Manager's
    // permission check gates each call before it runs. Returns are plain strings so any client can read
    // them without sharing the Ical.Net types.
    //
    // "Calendars" are named groups of events. An event belongs to one through its iCal CATEGORIES
    // value; events without one are in the built-in "default" calendar. The names and colours of
    // calendars live in Calendar/calendars.json next to the .ics files.
    public static class CalendarCapabilities
    {
        public const string DefaultId = "default";
        private const string DefaultColor = "#4F8EF7";
        private static readonly string[] Palette = { "#E5484D", "#30A46C", "#F5A524", "#8E4EC6", "#0091FF", "#D6409F", "#12A594" };

        private sealed record CalendarInfo(string Id, string Name, string Color);

        // Uniform health probe, one per worker (Calendar.Ping, Alarm.Ping, ...). The Diagnostics
        // harness invokes it to confirm this worker is up and routable without touching real data.
        [SeaOfDirac("Calendar.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Calendar: " + input;

        /// <summary>Events on one day, optionally only those in one calendar.</summary>
        [SeaOfDirac("GetEvents", new[] { "day", "calendarId" }, typeof(string), typeof(string), typeof(string))]
        public static string GetEvents(string day, string calendarId = "")
        {
            var when = DateTime.Parse(day);
            var events = CalendarClass.GetEventsForDay(when)
                .Where(e => string.IsNullOrEmpty(calendarId) || CalendarOf(e) == calendarId)
                .ToList();
            if (events.Count == 0) return $"No events on {day}.";
            return string.Join("; ", events.Select(e => $"{e.Start?.Value:HH:mm} {e.Summary}"));
        }

        /// <summary>Adds a one-hour event and returns its uid. Older callers send only day + summary.</summary>
        [SeaOfDirac("AddEvent", new[] { "day", "summary", "calendarId" }, typeof(string), typeof(string), typeof(string), typeof(string))]
        public static async Task<string> AddEvent(string day, string summary, string calendarId = "")
        {
            var when = DateTime.Parse(day);
            string uid = await CalendarClass.CreateSimpleEvent(when, summary, description: "", durationHours: 1);
            if (!string.IsNullOrEmpty(calendarId) && calendarId != DefaultId)
                await CalendarClass.UpdateEventByUid(uid, e => e.Categories = new List<string> { calendarId });
            return uid;
        }

        // Privileged: deleting an event is a capability an app must be granted separately. The sample
        // app is given GetEvents + AddEvent but NOT this, so calling it shows the broker's refusal.
        [SeaOfDirac("DeleteEvent", new[] { "uid" }, typeof(string), typeof(string))]
        public static string DeleteEvent(string uid)
        {
            CalendarClass.DeleteEventByUid(uid);
            return $"Deleted {uid}.";
        }

        /// <summary>"id|name|color;..." - the default calendar, the named ones, and any category an event uses.</summary>
        [SeaOfDirac("ListCalendars", new string[0], typeof(string))]
        public static string ListCalendars()
        {
            var all = new List<CalendarInfo> { new(DefaultId, "Calendar", DefaultColor) };
            all.AddRange(LoadNamed());
            foreach (var id in CalendarClass.LoadAllEvents().Select(CalendarOf).Distinct())
                if (!all.Any(c => c.Id == id)) all.Add(new CalendarInfo(id, id, ColorFor(id)));
            return string.Join(";", all.Select(c => $"{c.Id}|{Field(c.Name)}|{c.Color}"));
        }

        /// <summary>Creates (or renames/recolours) a calendar. Returns its id.</summary>
        [SeaOfDirac("CreateCalendar", new[] { "name", "color" }, typeof(string), typeof(string), typeof(string))]
        public static string CreateCalendar(string name, string color = "")
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A calendar needs a name.");
            string id = new string(name.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
            if (id.Length == 0 || id == DefaultId) throw new ArgumentException($"'{name}' can't be used as a calendar name.");

            var named = LoadNamed();
            named.RemoveAll(c => c.Id == id);
            named.Add(new CalendarInfo(id, name.Trim(), string.IsNullOrWhiteSpace(color) ? ColorFor(id) : color.Trim()));
            File.WriteAllText(NamedPath, JsonSerializer.Serialize(named));
            return id;
        }

        private static string CalendarOf(CalendarEvent e) => e.Categories?.FirstOrDefault() is { Length: > 0 } c ? c : DefaultId;

        private static string NamedPath =>
            Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "Calendar", "calendars.json");

        private static List<CalendarInfo> LoadNamed()
        {
            try { return File.Exists(NamedPath) ? JsonSerializer.Deserialize<List<CalendarInfo>>(File.ReadAllText(NamedPath)) ?? new() : new(); }
            catch (JsonException) { return new(); }
        }

        private static string ColorFor(string id) => Palette[(int)((uint)StableHash(id) % (uint)Palette.Length)];

        // string.GetHashCode is randomized per process; colours should stay put between runs.
        private static int StableHash(string s)
        {
            unchecked
            {
                int h = 23;
                foreach (char c in s) h = h * 31 + c;
                return h;
            }
        }

        // The wire format splits on ';' and '|', so neither may appear inside a field.
        private static string Field(string s) => s.Replace('|', '/').Replace(';', ',');
    }
}
