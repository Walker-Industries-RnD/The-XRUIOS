using System.Threading.Tasks;

namespace XRUIOS.Apps.Shared.Holes;

/// <summary>Hole: current + forecast weather. Backed by XRUIOS.Worker.Geo (locality) + a weather cap.</summary>
public static class WeatherFeed
{
    public sealed record Snapshot(string Location, int TempC, string Condition, int High, int Low);

    public static async Task<Snapshot?> CurrentAsync(string? overrideCity = null)
    {
        var raw = await AppShell.Broker.InvokeAsync<string>("Current",
            new() { ["city"] = overrideCity ?? "" });
        var p = raw.Split('|');
        if (p.Length < 5) return null;
        return new Snapshot(
            p[0],
            int.TryParse(p[1], out var t) ? t : 0,
            p[2],
            int.TryParse(p[3], out var hi) ? hi : 0,
            int.TryParse(p[4], out var lo) ? lo : 0);
    }
}
