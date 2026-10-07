using System.Reflection;
using System.Text.Json.Nodes;
using XRUIOS.Interfaces;
using YuukoProtocol;
using static Pariah_Cybersecurity.DataHandler;

// XRUIOS RecentlyRecorded worker. Owns only its class's store; other programs reach it through the Manager
// broker with permission checks. Take the Manager context, then run the secured host.
global::XRUIOS.Barebones.XRUIOS.BindFromEnvironment();

// This worker's own init step: seed the list (the old monolithic InitiateRecentlyRecorded did this).
string dir = Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "RecentlyRecorded");
Directory.CreateDirectory(dir);
if (!File.Exists(Path.Combine(dir, "RecentlyRecorded.json")))
{
    await JSONDataHandler.CreateJsonFile("RecentlyRecorded", dir, new JsonObject());
    var store = await JSONDataHandler.LoadJsonFile("RecentlyRecorded", dir);
    store = await JSONDataHandler.AddToJson<List<FileRecord>>(store, "Data", new List<FileRecord>(), global::XRUIOS.Barebones.XRUIOS.encryptionKey);
    await JSONDataHandler.SaveJson(store);
}

const string ServerName = "XRUIOS.Worker.RecentlyRecorded";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly());
