using System.Collections.Generic;
using System.Threading.Tasks;

namespace XRUIOS.Apps.Shared.Holes;

/// <summary>Hole: choose an on-screen-keyboard layout. Backed by XRUIOS.Worker.DeviceManager.</summary>
public static class KeyboardLayoutPicker
{
    public sealed record Layout(string Id, string DisplayName);

    public static async Task<IReadOnlyList<Layout>> ListAsync()
    {
        var raw = await AppShell.Broker.InvokeAsync<string>("ListKeyboardLayouts");
        var results = new List<Layout>();
        if (string.IsNullOrWhiteSpace(raw)) return results;
        foreach (var row in raw.Split(';'))
        {
            var p = row.Split('|');
            if (p.Length >= 2) results.Add(new Layout(p[0], p[1]));
        }
        return results;
    }

    public static Task<string> ApplyAsync(string layoutId)
        => AppShell.Broker.InvokeAsync<string>("ApplyKeyboardLayout", new() { ["layoutId"] = layoutId });
}
