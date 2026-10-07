using System.Reflection;
using System.Text.Json.Nodes;
using XRUIOS.Interfaces;
using YuukoProtocol;
using static Pariah_Cybersecurity.DataHandler;
using Models = XRUIOS.Barebones.Interfaces.Songs.Playlists;

// XRUIOS Songs worker. Owns only its class's store; other programs reach it through the Manager
// broker with permission checks. Take the Manager context, then run the secured host.
global::XRUIOS.Barebones.XRUIOS.BindFromEnvironment();

// This worker's own init step (the old monolithic InitiatMusic did this): the music folders, favorites
// and history lists, and the playlist store.
string music = Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "Music");
Directory.CreateDirectory(music);
Directory.CreateDirectory(Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "Music Metadata"));

async Task Seed<T>(string dir, string name, string variable, T empty)
{
    Directory.CreateDirectory(dir);
    if (File.Exists(Path.Combine(dir, name + ".json"))) return;
    await JSONDataHandler.CreateJsonFile(name, dir, new JsonObject());
    var store = await JSONDataHandler.LoadJsonFile(name, dir);
    store = await JSONDataHandler.AddToJson<T>(store, variable, empty, global::XRUIOS.Barebones.XRUIOS.encryptionKey);
    await JSONDataHandler.SaveJson(store);
}

await Seed(music, "MusicDirectory", "Data", new List<DirectoryRecord>());
await Seed(music, "MusicFavorites", "Data", new List<FileRecord>());
await Seed(music, "MusicHistory", "Data", new List<FileRecord>());
// The playlist code keeps its list in Music/Playlists under the variable name "Playlists".
await Seed(Path.Combine(music, "Playlists"), "Playlists", "Playlists", new List<Models.Playlist>());

const string ServerName = "XRUIOS.Worker.Songs";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly());
