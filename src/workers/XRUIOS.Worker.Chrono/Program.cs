using System.Reflection;
using XRUIOS.Interfaces;

// XRUIOS Chrono worker. Owns only its class's store; other programs reach it through the Manager
// broker with permission checks. Take the Manager context, then run the secured host.
global::XRUIOS.Barebones.XRUIOS.BindFromEnvironment();

// The user's clock settings (format, time zone). The first run has none saved; the defaults are the
// machine's own time zone and a 24-hour clock.
if (File.Exists(Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "Chrono", "Chrono.json")))
    await global::XRUIOS.Barebones.ChronoClass.LoadDateData();

const string ServerName = "XRUIOS.Worker.Chrono";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly());
