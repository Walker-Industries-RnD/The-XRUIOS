
using static XRUIOS.Barebones.Interfaces.ProcessesClass;

namespace XRUIOS.Barebones.Interfaces
{
    public static class WorldEventsClass
    {

        public record WorldEvent
        {
            public string DeviceId { get; init; }
            public DateTime Timestamp { get; init; }
            public string Action { get; init; }
            public string SceneDescription { get; init; }
            public string? EventId { get; init; }
            public string? WorldPointIdentifier { get; init; }

            public ProcessSnapshot? Snapshot { get; init; }

            public WorldEvent()
            {
                DeviceId = string.Empty;
                Timestamp = DateTime.UtcNow;
                Action = string.Empty;
                SceneDescription = string.Empty;
                EventId = Guid.NewGuid().ToString();
                WorldPointIdentifier = null;
                Snapshot = null;
            }

            public WorldEvent(
                string deviceId,
                DateTime timestamp,
                string action,
                string sceneDescription,
                string? eventId = null,
                string? worldPointIdentifier = null,
                ProcessSnapshot snapshot = null)
            {
                DeviceId = deviceId;
                Timestamp = timestamp.Kind != DateTimeKind.Utc ? timestamp.ToUniversalTime() : timestamp;
                Action = action;
                SceneDescription = sceneDescription;
                EventId = eventId ?? Guid.NewGuid().ToString();
                WorldPointIdentifier = worldPointIdentifier;
                Snapshot = snapshot;
            }
        }
    
    }
}