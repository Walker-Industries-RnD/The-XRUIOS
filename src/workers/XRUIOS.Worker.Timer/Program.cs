using System.Reflection;
using Hangfire;
using Hangfire.MemoryStorage;
using XRUIOS.Interfaces;

// XRUIOS Timer worker. Owns only its class's store; other programs reach it through the Manager
// broker with permission checks. Take the Manager context, then run the secured host.
global::XRUIOS.Barebones.XRUIOS.BindFromEnvironment();

// Timers are scheduled with Hangfire. Memory storage (timers do not survive a restart), one worker
// thread, and a one-second poll so a timer finishes when it should instead of up to 15 seconds late.
GlobalConfiguration.Configuration
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseMemoryStorage();
using var scheduler = new BackgroundJobServer(new BackgroundJobServerOptions
{
    WorkerCount = 1,
    SchedulePollingInterval = TimeSpan.FromSeconds(1),
});

const string ServerName = "XRUIOS.Worker.Timer";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly());
