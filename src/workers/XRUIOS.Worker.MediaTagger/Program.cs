using System.Reflection;
using System.Text.Json.Nodes;
using XRUIOS.Interfaces;
using static Pariah_Cybersecurity.DataHandler;

// XRUIOS MediaTagger worker. Owns only its class's store; other programs reach it through the Manager
// broker with permission checks. Take the Manager context, then run the secured host.
global::XRUIOS.Barebones.XRUIOS.BindFromEnvironment();

// This worker's own init step: the folder for the "Media" tags and their (empty) favorites list.
string dir = Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "Creators", "Media");
Directory.CreateDirectory(dir);
if (!File.Exists(Path.Combine(dir, "CreatorFavorites.json")))
{
    await JSONDataHandler.CreateJsonFile("CreatorFavorites", dir, new JsonObject());
    var store = await JSONDataHandler.LoadJsonFile("CreatorFavorites", dir);
    store = await JSONDataHandler.AddToJson<List<string>>(store, "Data", new List<string>(), global::XRUIOS.Barebones.XRUIOS.encryptionKey);
    await JSONDataHandler.SaveJson(store);
}

const string ServerName = "XRUIOS.Worker.MediaTagger";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly());
