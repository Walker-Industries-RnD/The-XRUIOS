using System.Reflection;
using XRUIOS.Interfaces;

// XRUIOS Note worker. Owns only its class's store; other programs reach it through the Manager
// broker with permission checks. Take the Manager context, then run the secured host.
global::XRUIOS.Barebones.XRUIOS.BindFromEnvironment();

// This worker's own init step: the folder journals are saved in. (The favorites list is created the
// first time a journal is favorited.)
Directory.CreateDirectory(Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "Journals"));

const string ServerName = "XRUIOS.Worker.Note";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly());
