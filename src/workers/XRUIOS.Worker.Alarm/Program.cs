using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json.Nodes;
using XRUIOS.Barebones;
using XRUIOS.Interfaces;
using XRUIOS.Worker.Alarm;
using static Pariah_Cybersecurity.DataHandler;
using AlarmRecord = XRUIOS.Barebones.Interfaces.AlarmClass.Alarm;

// XRUIOS Alarm worker. Owns only its class's store; other programs reach it through the Manager
// broker with permission checks. Take the Manager context, then run the secured host.
global::XRUIOS.Barebones.XRUIOS.BindFromEnvironment();

// This worker's own init step: seed the alarm store (the old monolithic init did this), start the
// scheduler, and load + schedule the saved alarms. Fires become AlarmFired events.
string alarmDir = Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "Alarms");
Directory.CreateDirectory(alarmDir);
if (!File.Exists(Path.Combine(alarmDir, "Alarms.json")))
{
    await JSONDataHandler.CreateJsonFile("Alarms", alarmDir, new JsonObject());
    var store = await JSONDataHandler.LoadJsonFile("Alarms", alarmDir);
    store = await JSONDataHandler.AddToJson<ObservableCollection<AlarmRecord>>(store, "Data", new ObservableCollection<AlarmRecord>(), global::XRUIOS.Barebones.XRUIOS.encryptionKey);
    await JSONDataHandler.SaveJson(store);
}

AlarmClass.OnAlarmFired += AlarmCapabilities.OnFired;
HangfireBootstrap.Start();
await AlarmClass.LoadAlarms();

const string ServerName = "XRUIOS.Worker.Alarm";

await SecureWorkerHost.Run(
    serverName: ServerName,
    capabilityAssembly: Assembly.GetExecutingAssembly());
