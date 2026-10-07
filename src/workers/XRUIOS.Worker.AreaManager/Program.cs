using System.Reflection;
using XRUIOS.Interfaces;

// XRUIOS AreaManager worker. Owns only its class's store; other programs reach it through the Manager
// broker with permission checks. Take the Manager context, then run the secured host.
global::XRUIOS.Barebones.XRUIOS.BindFromEnvironment();

// This worker's own init step: the folder world points are saved in (one file per point).
Directory.CreateDirectory(Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "WorldPoint"));

const string ServerName = "XRUIOS.Worker.AreaManager";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly());
