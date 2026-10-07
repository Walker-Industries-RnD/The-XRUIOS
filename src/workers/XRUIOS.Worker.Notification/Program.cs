using System.Reflection;
using System.Text.Json.Nodes;
using XRUIOS.Interfaces;
using static Pariah_Cybersecurity.DataHandler;
using static XRUIOS.Barebones.Interfaces.NotificationClass;

// XRUIOS Notification worker. Owns only its class's store; other programs reach it through the Manager
// broker with permission checks. Take the Manager context, then run the secured host.
global::XRUIOS.Barebones.XRUIOS.BindFromEnvironment();

// This worker's own init step: seed the history store (the old monolithic InitiateNotifications did this).
string historyDir = Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "NotificationHistory");
Directory.CreateDirectory(historyDir);
if (!File.Exists(Path.Combine(historyDir, "NotificationHistory.json")))
{
    await JSONDataHandler.CreateJsonFile("NotificationHistory", historyDir, new JsonObject());
    var store = await JSONDataHandler.LoadJsonFile("NotificationHistory", historyDir);
    store = await JSONDataHandler.AddToJson<List<NotificationContent>>(store, "Data", new List<NotificationContent>(), global::XRUIOS.Barebones.XRUIOS.encryptionKey);
    await JSONDataHandler.SaveJson(store);
}

const string ServerName = "XRUIOS.Worker.Notification";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly());
