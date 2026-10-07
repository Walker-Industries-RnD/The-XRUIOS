using System.Threading.Tasks;

namespace XRUIOS.Apps.Shared;

/// <summary>
/// One place every app grabs the broker from. Photino host sets this before the
/// OpenSilver App starts up, so any page can do <c>AppShell.Broker.InvokeAsync(...)</c>
/// without threading the reference through XAML.
/// </summary>
public static class AppShell
{
    public static WorkerBroker Broker { get; private set; } = null!;

    /// <summary>True after <see cref="InitializeAsync"/> completes.</summary>
    public static bool IsReady { get; private set; }

    public static async Task InitializeAsync(string appName)
    {
        if (IsReady) return;
        Broker = await WorkerBroker.ConnectAsync(appName);
        IsReady = true;
    }
}
