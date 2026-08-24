using System.Reflection;
using XRUIOS.Interfaces;

// XRUIOS Plagues worker — secured boot sequence:
//   1. Pariah AntiTamper hardens the process (blocks DLL injection, watches for a debugger).
//   2. NotaryGuard Blake3-verifies the worker's on-disk folder BEFORE it serves anything.
//   3. Eclipse stands up an AES-256-GCM + Kyber encrypted channel (enroll -> handshake -> invoke).
//   4. WorkerOcean scans this assembly for [SeaOfDirac] capabilities and dispatches to them.
//
// The worker trusts ONLY the XRUIOS.Manager (which alone holds its PSK, handed over at launch via
// XRUIOS_WORKER_PSK). Per-app permission decisions — who may call which capability — are enforced
// upstream at the Manager broker via XRUIOS.Permission, so the LOCAL gate is allow-all.

const string ServerName = "test";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly(),
    gate: new AllowAllPermissionGate(),
    guard: NotaryGuard.ForCurrentWorker(ServerName));
