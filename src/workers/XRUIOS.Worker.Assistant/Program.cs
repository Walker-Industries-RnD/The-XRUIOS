using System.Reflection;
using XRUIOS.Interfaces;

// XRUIOS Assistant worker. Owns only PocketPal's question/answer history; other programs reach it
// through the Manager broker with permission checks. Take the Manager context, then run the secured host.
global::XRUIOS.Barebones.XRUIOS.BindFromEnvironment();

const string ServerName = "XRUIOS.Worker.Assistant";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly());
