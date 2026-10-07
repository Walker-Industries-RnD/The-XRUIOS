using System.Collections.Generic;
using System.Threading.Tasks;

namespace XRUIOS.Apps.Shared.Holes;

/// <summary>
/// Hole: "load / save / choose a theme". Theme Editor writes; every other app
/// reads through <see cref="ObserveActiveAsync"/> so a theme change repaints
/// the widget without a restart.
/// Backed by XRUIOS.Worker.Theme.
/// </summary>
public static class ThemePicker
{
    public sealed record Theme(string Id, string DisplayName, string PreviewSwatch);

    public static async Task<IReadOnlyList<Theme>> ListAsync()
    {
        var raw = await AppShell.Broker.InvokeAsync<string>("ListThemes");
        var results = new List<Theme>();
        if (string.IsNullOrWhiteSpace(raw)) return results;
        foreach (var row in raw.Split(';'))
        {
            var parts = row.Split('|');
            if (parts.Length >= 3) results.Add(new Theme(parts[0], parts[1], parts[2]));
        }
        return results;
    }

    public static Task<string> ApplyAsync(string themeId)
        => AppShell.Broker.InvokeAsync<string>("ApplyTheme", new() { ["themeId"] = themeId });

    public static Task<string> GetActiveAsync()
        => AppShell.Broker.InvokeAsync<string>("GetActiveTheme");

    /// <summary>
    /// One-shot for the widget-repaint hole; UIs poll this on a short cadence
    /// instead of holding a subscription (keeps the WASM working set flat).
    /// </summary>
    public static async Task ObserveActiveAsync(System.Action<string> onChanged)
    {
        var last = await GetActiveAsync();
        onChanged(last);
    }
}
