using System.Reflection;
using XRUIOS.Interfaces;

// XRUIOS Creator worker. Owns only its class's store; other programs reach it through the Manager
// broker with permission checks. Take the Manager context, then run the secured host.
global::XRUIOS.Barebones.XRUIOS.BindFromEnvironment();

// This worker's own init step: the folder creators are saved in. (Each creator type gets its favorites
// list the first time a creator of that type is made.)
Directory.CreateDirectory(Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "Creators"));

const string ServerName = "XRUIOS.Worker.Creator";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly());
