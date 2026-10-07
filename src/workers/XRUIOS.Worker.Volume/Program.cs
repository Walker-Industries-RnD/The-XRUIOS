using System.Reflection;
using System.Text.Json.Nodes;
using XRUIOS.Interfaces;
using static Pariah_Cybersecurity.DataHandler;
using static XRUIOS.Barebones.Interfaces.VolumeClass;

// XRUIOS Volume worker. Owns only its class's store; other programs reach it through the Manager
// broker with permission checks. Take the Manager context, then run the secured host.
global::XRUIOS.Barebones.XRUIOS.BindFromEnvironment();

// This worker's own init step: seed the saved-mixes store (the old monolithic InitiateVolume did this).
string mixDir = Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "VolumeMixings");
Directory.CreateDirectory(mixDir);
if (!File.Exists(Path.Combine(mixDir, "VolumeMixings.json")))
{
    await JSONDataHandler.CreateJsonFile("VolumeMixings", mixDir, new JsonObject());
    var store = await JSONDataHandler.LoadJsonFile("VolumeMixings", mixDir);
    store = await JSONDataHandler.AddToJson<List<VolumeSetting>>(store, "VolumeMixings", new List<VolumeSetting>(), global::XRUIOS.Barebones.XRUIOS.encryptionKey);
    await JSONDataHandler.SaveJson(store);
}

const string ServerName = "XRUIOS.Worker.Volume";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly());
