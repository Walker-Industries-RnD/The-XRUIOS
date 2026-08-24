using System.Reflection;
using XRUIOS.Interfaces;

// XRUIOS Calendar worker. It owns the calendar store; other programs reach events only through the
// Manager broker with the Time.Calendar capabilities, never in-process. Boot order: take the context
// the Manager handed us, then run the secured host (hardening + Notary check + PSK lock + serve).
global::XRUIOS.Barebones.XRUIOS.BindFromEnvironment();

// This worker's own init step: make sure its store directory exists before it serves. (CalendarClass
// assumes the old monolithic InitializeSystemAsync ran first; here each worker sets up its own.)
System.IO.Directory.CreateDirectory(
    System.IO.Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "Calendar"));

const string ServerName = "XRUIOS.Worker.Calendar";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly(),
    gate: new AllowAllPermissionGate(),
    guard: NotaryGuard.ForCurrentWorker(ServerName));
