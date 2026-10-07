using System.Reflection;
using System.Text.Json.Nodes;
using XRUIOS.Interfaces;
using static Pariah_Cybersecurity.DataHandler;
using EqPreset = XRUIOS.Barebones.Interfaces.SoundEQClass.SoundEQ;
using ExperimentalAudio = XRUIOS.Barebones.Interfaces.ExperimentalAudioClass.ExperimentalAudio;

// XRUIOS SoundEQ worker. Owns only its class's store; other programs reach it through the Manager
// broker with permission checks. Take the Manager context, then run the secured host.
global::XRUIOS.Barebones.XRUIOS.BindFromEnvironment();

// This worker's own init step: seed the preset store with the flat "Default" preset (as the old
// monolithic InitiateSoundEQDB did).
string eqDir = Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "EQDB");
Directory.CreateDirectory(eqDir);
if (!File.Exists(Path.Combine(eqDir, "EQDBData.json")))
{
    var flat = new EqPreset("Default", 100, 100, 100, 100, 100, 100, 100, new ExperimentalAudio(false, false, 0, 0));
    await JSONDataHandler.CreateJsonFile("EQDBData", eqDir, new JsonObject());
    var store = await JSONDataHandler.LoadJsonFile("EQDBData", eqDir);
    store = await JSONDataHandler.AddToJson<List<EqPreset>>(store, "Data", new List<EqPreset> { flat }, global::XRUIOS.Barebones.XRUIOS.encryptionKey);
    store = await JSONDataHandler.AddToJson<EqPreset>(store, "DefaultEQDB", flat, global::XRUIOS.Barebones.XRUIOS.encryptionKey);
    await JSONDataHandler.SaveJson(store);
}

const string ServerName = "XRUIOS.Worker.SoundEQ";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly());
