using System.Reflection;
using XRUIOS.Interfaces;

// XRUIOS Clipboard worker. Owns only its class's store; other programs reach it through the Manager
// broker with permission checks. Take the Manager context, then run the secured host.
global::XRUIOS.Barebones.XRUIOS.BindFromEnvironment();

const string ServerName = "XRUIOS.Worker.Clipboard";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly(),
    gate: new AllowAllPermissionGate(),
    guard: NotaryGuard.ForCurrentWorker(ServerName));
