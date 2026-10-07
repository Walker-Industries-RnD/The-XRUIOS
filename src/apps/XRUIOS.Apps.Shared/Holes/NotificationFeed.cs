using System.Collections.Generic;
using System.Threading.Tasks;

namespace XRUIOS.Apps.Shared.Holes;

/// <summary>Hole: read / dismiss notifications. Backed by XRUIOS.Worker.Notification.</summary>
public static class NotificationFeed
{
    public sealed record Notice(string Id, string Source, string Title, string Body, string Timestamp);

    public static async Task<IReadOnlyList<Notice>> UnreadAsync()
    {
        var raw = await AppShell.Broker.InvokeAsync<string>("Unread");
        var results = new List<Notice>();
        if (string.IsNullOrWhiteSpace(raw)) return results;
        foreach (var row in raw.Split(';'))
        {
            var p = row.Split('|');
            if (p.Length >= 5) results.Add(new Notice(p[0], p[1], p[2], p[3], p[4]));
        }
        return results;
    }

    public static Task<string> DismissAsync(string id)
        => AppShell.Broker.InvokeAsync<string>("Dismiss", new() { ["id"] = id });
}
