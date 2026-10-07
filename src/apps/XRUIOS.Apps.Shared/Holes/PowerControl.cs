using System.Threading.Tasks;

namespace XRUIOS.Apps.Shared.Holes;

/// <summary>Hole: power menu (shutdown / restart / sleep). Backed by XRUIOS.Worker.SystemInfo.</summary>
public static class PowerControl
{
    public enum PowerAction { Shutdown, Restart, Sleep, Hibernate, SignOut, Lock }

    public static Task<string> RequestAsync(PowerAction action)
        => AppShell.Broker.InvokeAsync<string>("PowerRequest", new() { ["action"] = action.ToString() });

    public static Task<string> BatteryAsync()
        => AppShell.Broker.InvokeAsync<string>("BatteryStatus");
}
