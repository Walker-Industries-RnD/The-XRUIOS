using System.Collections.Generic;
using System.Threading.Tasks;

namespace XRUIOS.Apps.Shared.Holes;

/// <summary>Hole: world / news events. Backed by XRUIOS.Worker.WorldEvents.</summary>
public static class WorldEventsFeed
{
    public sealed record Story(string Id, string Headline, string Source, string Region);

    public static async Task<IReadOnlyList<Story>> TodayAsync()
    {
        var raw = await AppShell.Broker.InvokeAsync<string>("Today");
        var results = new List<Story>();
        if (string.IsNullOrWhiteSpace(raw)) return results;
        foreach (var row in raw.Split(';'))
        {
            var p = row.Split('|');
            if (p.Length >= 4) results.Add(new Story(p[0], p[1], p[2], p[3]));
        }
        return results;
    }
}
