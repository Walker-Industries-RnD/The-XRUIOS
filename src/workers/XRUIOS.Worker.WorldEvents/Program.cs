using System.Reflection;
using System.Text.Json.Nodes;
using XRUIOS.Interfaces;
using static Pariah_Cybersecurity.DataHandler;
using WorldEvent = XRUIOS.Barebones.Interfaces.WorldEventsClass.WorldEvent;

// XRUIOS WorldEvents worker. Owns only its class's store; other programs reach it through the Manager
// broker with permission checks. Take the Manager context, then run the secured host.
global::XRUIOS.Barebones.XRUIOS.BindFromEnvironment();

// This worker's own init step: seed the event log (the old monolithic InitiateWorldEvents did this).
string eventsDir = Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "WorldEvents");
Directory.CreateDirectory(eventsDir);
if (!File.Exists(Path.Combine(eventsDir, "WorldEvents.json")))
{
    await JSONDataHandler.CreateJsonFile("WorldEvents", eventsDir, new JsonObject());
    var store = await JSONDataHandler.LoadJsonFile("WorldEvents", eventsDir);
    store = await JSONDataHandler.AddToJson<List<WorldEvent>>(store, "WorldEvents", new List<WorldEvent>(), global::XRUIOS.Barebones.XRUIOS.encryptionKey);
    await JSONDataHandler.SaveJson(store);
}

const string ServerName = "XRUIOS.Worker.WorldEvents";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly());
