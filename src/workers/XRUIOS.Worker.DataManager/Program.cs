using System.Reflection;
using XRUIOS.Interfaces;

// XRUIOS DataManager worker. Owns only its class's store; other programs reach it through the Manager
// broker with permission checks. Take the Manager context, then run the secured host.
global::XRUIOS.Barebones.XRUIOS.BindFromEnvironment();

// This worker's own init step: the folders sessions and data slots are saved in (one folder each).
Directory.CreateDirectory(Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "Session"));
Directory.CreateDirectory(Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "DataSlot"));

const string ServerName = "XRUIOS.Worker.DataManager";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly());
