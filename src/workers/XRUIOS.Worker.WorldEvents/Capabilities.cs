using EclipseProject;
using XRUIOS.Interfaces;
using XRUIOS.Interpreter;
using WorldEvent = XRUIOS.Barebones.Interfaces.WorldEventsClass.WorldEvent;
using WorldEventStore = XRUIOS.Barebones.WorldEventsClass;

namespace XRUIOS.Worker.WorldEvents
{
    // The log of things that happened in the user's spaces. Whatever records them (a scene tracker, a
    // game, a provider app) pushes them in with AddWorldEvent; each one is announced as WorldEventAdded.
    // Today is what the WorldEventsFeed hole calls.
    public static class WorldEventsCapabilities
    {
        [SeaOfDirac("WorldEvents.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.WorldEvents: " + input;

        /// <summary>"id|headline|source|region;..." for today's events, newest first.</summary>
        [SeaOfDirac("Today", new string[0], typeof(string))]
        public static async Task<string> Today()
        {
            var today = DateTime.Now.Date;
            var rows = (await WorldEventStore.GetWorldEvents())
                .Where(e => e.Timestamp.ToLocalTime().Date == today)
                .OrderByDescending(e => e.Timestamp)
                .Select(e => $"{Wire.Field(e.EventId)}|{Wire.Field(e.Action)}|{Wire.Field(e.DeviceId)}|{Wire.Field(e.WorldPointIdentifier)}");
            return string.Join(';', rows);
        }

        /// <summary>Record an event from this device. Returns its id.</summary>
        [SeaOfDirac("AddWorldEvent", new[] { "action", "description", "worldPoint" },
            typeof(string), typeof(string), typeof(string), typeof(string))]
        public static async Task<string> AddWorldEvent(string action, string description, string worldPoint)
        {
            if (string.IsNullOrWhiteSpace(action)) throw new ArgumentException("An event needs an action.");
            var e = new WorldEvent(Environment.MachineName, DateTime.UtcNow, action, description ?? "",
                worldPointIdentifier: string.IsNullOrWhiteSpace(worldPoint) ? null : worldPoint);
            await WorldEventStore.AddWorldEvent(e);

            WorkerEvents.Publish(XruiosEventNames.WorldEventAdded, EventJson.Write(new WorldEventInfo(
                e.EventId!, e.Action, e.SceneDescription, e.DeviceId, e.WorldPointIdentifier, e.Timestamp)));
            return e.EventId!;
        }
    }
}
