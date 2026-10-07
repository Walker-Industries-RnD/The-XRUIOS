using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace XRUIOS.Apps.Shared.Holes;

/// <summary>Hole: clock / stopwatch / alarm state. Backed by XRUIOS.Worker.Chrono + .Alarm + .Stopwatch.</summary>
public static class TimeSource
{
    /// <summary>The clock in the user's chosen formats.</summary>
    public sealed record Clock(string ShortTime, string ShortDate, string LongTime, string LongDate, string TimeZone, DateTime At);

    public sealed record Lap(int Number, int SecondsElapsed);

    public static async Task<Clock?> ClockAsync(string? timezone = null)
    {
        var p = (await AppShell.Broker.InvokeAsync<string>("Now", new() { ["tz"] = timezone ?? "" })).Split('|');
        if (p.Length < 6) return null;
        return new Clock(p[0], p[1], p[2], p[3], p[4], DateTime.TryParse(p[5], out var at) ? at : DateTime.Now);
    }

    /// <summary>"short time  short date", ready to show.</summary>
    public static async Task<string> NowAsync(string? timezone = null)
    {
        var c = await ClockAsync(timezone);
        return c is null ? "" : $"{c.ShortTime}  {c.ShortDate}";
    }

    public static Task<string> ListAlarmsAsync()
        => AppShell.Broker.InvokeAsync<string>("ListAlarms");

    public static Task<string> AddAlarmAsync(string whenIso, string label)
        => AppShell.Broker.InvokeAsync<string>("AddAlarm", new() { ["when"] = whenIso, ["label"] = label });

    public static Task<string> DeleteAlarmAsync(string label)
        => AppShell.Broker.InvokeAsync<string>("DeleteAlarm", new() { ["label"] = label });

    /// <summary>Starts a stopwatch and returns its id.</summary>
    public static Task<string> StartStopwatchAsync()
        => AppShell.Broker.InvokeAsync<string>("StopwatchStart");

    /// <summary>"hh:mm:ss.fff".</summary>
    public static Task<string> StopwatchElapsedAsync(string id)
        => AppShell.Broker.InvokeAsync<string>("StopwatchElapsed", new() { ["id"] = id });

    public static async Task<Lap> StopwatchLapAsync(string id)
    {
        var p = (await AppShell.Broker.InvokeAsync<string>("StopwatchLap", new() { ["id"] = id })).Split('|');
        return new Lap(int.Parse(p[0]), int.Parse(p[1]));
    }

    /// <summary>Stops it; returns the final time and every lap.</summary>
    public static async Task<(string Total, IReadOnlyList<Lap> Laps)> StopStopwatchAsync(string id)
    {
        var rows = (await AppShell.Broker.InvokeAsync<string>("StopwatchStop", new() { ["id"] = id })).Split(';');
        var laps = new List<Lap>();
        for (int i = 1; i < rows.Length; i++)
        {
            var p = rows[i].Split('|');
            if (p.Length == 2) laps.Add(new Lap(int.Parse(p[0]), int.Parse(p[1])));
        }
        return (rows[0], laps);
    }
}
