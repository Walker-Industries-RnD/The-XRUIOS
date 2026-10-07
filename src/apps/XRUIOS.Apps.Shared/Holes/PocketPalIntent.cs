using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XRUIOS.Interfaces;
using XRUIOS.Interpreter;

namespace XRUIOS.Apps.Shared.Holes;

/// <summary>
/// Hole: ask PocketPal (the local assistant). Backed by XRUIOS.Worker.Assistant; the answer comes from
/// whichever provider app the user installed (see <see cref="AssistantProvider"/>).
/// </summary>
public static class PocketPalIntent
{
    public sealed record Turn(string Id, string Prompt, string Answer, DateTime AskedAt)
    {
        public bool Pending => Answer.Length == 0;
    }

    /// <summary>
    /// The answer, or "pending|&lt;id&gt;" when no provider replied in time. A late answer still
    /// arrives through <see cref="OnAnswered"/>.
    /// </summary>
    public static Task<string> AskAsync(string prompt)
        => AppShell.Broker.InvokeAsync<string>("Ask", new() { ["prompt"] = prompt });

    /// <summary>Newest first.</summary>
    public static async Task<IReadOnlyList<Turn>> HistoryAsync(int count = 20)
    {
        var raw = await AppShell.Broker.InvokeAsync<string>("AssistantHistory", new() { ["count"] = (long)count });
        var turns = new List<Turn>();
        if (string.IsNullOrWhiteSpace(raw)) return turns;
        foreach (var row in raw.Split(';'))
        {
            var p = row.Split('|');
            if (p.Length >= 4)
                turns.Add(new Turn(p[0], p[1], p[2], DateTime.TryParse(p[3], out var at) ? at : DateTime.MinValue));
        }
        return turns;
    }

    /// <summary>The raw history string (kept for pages that show it as-is).</summary>
    public static Task<string> RecentAsync()
        => AppShell.Broker.InvokeAsync<string>("AssistantHistory");

    /// <summary>Answers as they arrive (needs Events.AssistantAnswered). Dispose to stop.</summary>
    public static XruiosEventStream OnAnswered(Action<AssistantAnswer> handler)
        => EventFeed.On(XruiosEventNames.AssistantAnswered, handler);
}
