using System.Collections.Generic;
using System.Threading.Tasks;

namespace XRUIOS.Apps.Shared.Holes;

/// <summary>
/// Hole: "pick a photo/video album". Gallery uses this to scope the browse
/// grid; Notifications uses it when the user attaches an image to a reply.
/// Backed by XRUIOS.Worker.MediaAlbum.
/// </summary>
public static class MediaAlbumPicker
{
    public sealed record Album(string Id, string Title, int Count);

    public static async Task<IReadOnlyList<Album>> ListAsync()
    {
        var raw = await AppShell.Broker.InvokeAsync<string>("ListAlbums");
        var results = new List<Album>();
        if (string.IsNullOrWhiteSpace(raw)) return results;
        foreach (var row in raw.Split(';'))
        {
            var parts = row.Split('|');
            if (parts.Length >= 3 && int.TryParse(parts[2], out var count))
                results.Add(new Album(parts[0], parts[1], count));
        }
        return results;
    }

    /// <summary>Creates an empty album; returns its id.</summary>
    public static Task<string> CreateAsync(string title, string description = "")
        => AppShell.Broker.InvokeAsync<string>("CreateAlbum", new() { ["name"] = title, ["description"] = description });
}
