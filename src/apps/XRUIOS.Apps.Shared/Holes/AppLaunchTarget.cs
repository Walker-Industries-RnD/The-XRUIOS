using System.Collections.Generic;
using System.Threading.Tasks;

namespace XRUIOS.Apps.Shared.Holes;

/// <summary>
/// Hole: "list installed apps / launch one". App Launcher fills its grid from
/// <see cref="ListAsync"/>; PocketPal calls <see cref="LaunchAsync"/> when the
/// user asks it to open something. Backed by XRUIOS.Worker.App.
/// </summary>
public static class AppLaunchTarget
{
    public sealed record App(string Id, string DisplayName, string IconPath);

    public static async Task<IReadOnlyList<App>> ListAsync()
    {
        var raw = await AppShell.Broker.InvokeAsync<string>("ListApps");
        var results = new List<App>();
        if (string.IsNullOrWhiteSpace(raw)) return results;
        foreach (var row in raw.Split(';'))
        {
            var parts = row.Split('|');
            if (parts.Length >= 3) results.Add(new App(parts[0], parts[1], parts[2]));
        }
        return results;
    }

    public static Task<string> LaunchAsync(string appId)
        => AppShell.Broker.InvokeAsync<string>("LaunchApp", new() { ["appId"] = appId });
}
