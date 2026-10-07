using System.Collections.Generic;
using System.Threading.Tasks;

namespace XRUIOS.Apps.Shared.Holes;

/// <summary>
/// Hole: "pick a calendar". Any app that needs to scope reads/writes to one of
/// the user's calendars calls <see cref="ListAsync"/>, hands the choices to a
/// picker UI, then keeps the returned id.
///
/// Backed by Time.Calendar capabilities on the Calendar worker.
/// </summary>
public static class CalendarPicker
{
    public sealed record Calendar(string Id, string DisplayName, string Color);

    public static async Task<IReadOnlyList<Calendar>> ListAsync()
    {
        // ListCalendars is served by XRUIOS.Worker.Calendar; the shape is
        // "id|name|color;id|name|color;...". Keeps the worker string-in/string-out.
        var raw = await AppShell.Broker.InvokeAsync<string>("ListCalendars");
        var results = new List<Calendar>();
        if (string.IsNullOrWhiteSpace(raw)) return results;
        foreach (var row in raw.Split(';'))
        {
            var parts = row.Split('|');
            if (parts.Length >= 3) results.Add(new Calendar(parts[0], parts[1], parts[2]));
        }
        return results;
    }

    /// <summary>Creates a calendar (or renames/recolours one); returns its id.</summary>
    public static Task<string> CreateAsync(string name, string color = "")
        => AppShell.Broker.InvokeAsync<string>("CreateCalendar", new() { ["name"] = name, ["color"] = color });

    /// <summary>One day's events, from one calendar or (null) all of them.</summary>
    public static Task<string> GetEventsAsync(string dayIso, string? calendarId = null)
        => AppShell.Broker.InvokeAsync<string>("GetEvents", new() { ["day"] = dayIso, ["calendarId"] = calendarId ?? "" });

    public static Task<string> AddEventAsync(string calendarId, string dayIso, string summary)
        => AppShell.Broker.InvokeAsync<string>("AddEvent", new()
        {
            ["calendarId"] = calendarId,
            ["day"] = dayIso,
            ["summary"] = summary,
        });
}
