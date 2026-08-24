using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace XRUIOS.Manager
{
    // Runs the Manager as a Windows Service (session 0, starts at boot, survives logout). On Linux the
    // systemd unit runs the plain `run` verb, so this path is Windows-only and falls back elsewhere.
    internal static class ServiceHost
    {
        public static async Task<int> RunAsync(string[] args)
        {
            if (!OperatingSystem.IsWindows())
                return await ManagerHost.RunAsync(args);

            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddWindowsService(o => o.ServiceName = "XRUIOS.Manager");
            builder.Services.AddHostedService<ManagerService>();
            await builder.Build().RunAsync();
            return 0;
        }

        // Ties the Manager's run loop to the service lifetime: a service stop cancels the token, which
        // flows into RunAsync and shuts the broker + workers down.
        private sealed class ManagerService : BackgroundService
        {
            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                await ManagerHost.RunAsync(System.Array.Empty<string>(), stoppingToken);
            }
        }
    }
}
