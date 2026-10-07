using System.Collections.Generic;
using System.Threading.Tasks;

namespace XRUIOS.Apps.Shared.Holes;

/// <summary>Hole: per-app volume mixer + master. Backed by XRUIOS.Worker.Volume.</summary>
public static class VolumeMixer
{
    public sealed record Channel(string Id, string Name, int Volume, bool Muted);

    public static async Task<IReadOnlyList<Channel>> ListAsync()
    {
        var raw = await AppShell.Broker.InvokeAsync<string>("ListChannels");
        var results = new List<Channel>();
        if (string.IsNullOrWhiteSpace(raw)) return results;
        foreach (var row in raw.Split(';'))
        {
            var p = row.Split('|');
            if (p.Length >= 4 && int.TryParse(p[2], out var v))
                results.Add(new Channel(p[0], p[1], v, p[3] == "1"));
        }
        return results;
    }

    public static Task<string> SetVolumeAsync(string channelId, int volume0to100)
        => AppShell.Broker.InvokeAsync<string>("SetVolume", new()
        {
            ["channelId"] = channelId,
            ["volume"] = volume0to100,
        });

    public static Task<string> ToggleMuteAsync(string channelId)
        => AppShell.Broker.InvokeAsync<string>("ToggleMute", new() { ["channelId"] = channelId });
}
