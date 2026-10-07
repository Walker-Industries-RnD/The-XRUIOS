using System.Collections.Generic;
using System.Threading.Tasks;

namespace XRUIOS.Apps.Shared.Holes;

/// <summary>Hole: read / pin clipboard history. Backed by XRUIOS.Worker.Clipboard.</summary>
public static class ClipboardHistory
{
    public sealed record Entry(string Id, string Kind, string Preview, string Timestamp);

    public static async Task<IReadOnlyList<Entry>> RecentAsync(int limit = 40)
    {
        var raw = await AppShell.Broker.InvokeAsync<string>("Recent", new() { ["limit"] = limit });
        var results = new List<Entry>();
        if (string.IsNullOrWhiteSpace(raw)) return results;
        foreach (var row in raw.Split(';'))
        {
            var p = row.Split('|');
            if (p.Length >= 4) results.Add(new Entry(p[0], p[1], p[2], p[3]));
        }
        return results;
    }

    public static Task<string> PinAsync(string id)
        => AppShell.Broker.InvokeAsync<string>("Pin", new() { ["id"] = id });

    public static Task<string> ShareAsync(string id, string target)
        => AppShell.Broker.InvokeAsync<string>("Share", new() { ["id"] = id, ["target"] = target });
}
