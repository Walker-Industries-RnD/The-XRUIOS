using System.Text.Json;
using System.Text.Json.Serialization;

namespace XRUIOS.Interfaces
{
    /// <summary>
    /// One thing that happened in the XRUIOS ("ThemeChanged", "AlarmFired", ...). Workers publish
    /// these; the Manager collects them and hands each app only the ones it's been granted
    /// ("Events.&lt;Name&gt;"). The payload is plain text - usually JSON - so any client can read it.
    /// </summary>
    public sealed record XruiosEvent(
        [property: JsonPropertyName("seq")] long Seq,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("payload")] string Payload,
        [property: JsonPropertyName("at")] DateTimeOffset At);

    /// <summary>A batch of events plus where to continue from. <see cref="Reset"/> = some were missed.</summary>
    public sealed record XruiosEventBatch(
        [property: JsonPropertyName("cursor")] long Cursor,
        [property: JsonPropertyName("reset")] bool Reset,
        [property: JsonPropertyName("events")] IReadOnlyList<XruiosEvent> Events,
        [property: JsonPropertyName("epoch")] string? Epoch = null)
    {
        public string ToJson() => JsonSerializer.Serialize(this);
        public static XruiosEventBatch FromJson(string json) =>
            JsonSerializer.Deserialize<XruiosEventBatch>(json) ?? throw new FormatException("Empty event batch.");
    }

    /// <summary>Names shared by both ends of the event channel.</summary>
    public static class XruiosEventProtocol
    {
        /// <summary>What an app calls on the Manager broker: args after (long), waitMs (long).</summary>
        public const string PollCapability = "Events.Poll";

        /// <summary>
        /// What the Manager calls on a worker. Deliberately NOT announced as a capability, so the
        /// broker can never route an app to it - only the Manager's own pump reaches it.
        /// </summary>
        public const string WorkerPollCapability = "__xruios.events";

        /// <summary>The permission an app needs to receive an event: "Events.ThemeChanged".</summary>
        public static string PermissionFor(string eventName) => "Events." + eventName;

        /// <summary>
        /// An app opens its event stream as a SECOND session under "&lt;appId&gt;#events" (same key),
        /// because a session carries one call at a time and a long poll would block its other calls.
        /// </summary>
        public const string StreamIdentitySuffix = "#events";

        public static string BaseIdentity(string clientId) =>
            clientId.EndsWith(StreamIdentitySuffix, StringComparison.Ordinal)
                ? clientId[..^StreamIdentitySuffix.Length]
                : clientId;

        public const int MaxWaitMs = 25_000;
    }

    /// <summary>
    /// Worker side: a bounded in-memory log of what this worker published. The Manager long-polls it
    /// over its own session; if the worker restarts, the new <see cref="Epoch"/> tells the Manager to
    /// read from the beginning of the new log instead of an old cursor.
    /// </summary>
    public static class WorkerEvents
    {
        private const int Capacity = 512;

        private static readonly object Gate = new();
        private static readonly List<XruiosEvent> Log = new();
        private static long _seq;
        private static TaskCompletionSource _changed = NewSignal();

        public static string Epoch { get; } = Guid.NewGuid().ToString("N");

        /// <summary>Set by SecureWorkerHost.Run to the worker's name.</summary>
        public static string Source { get; internal set; } = AppDomain.CurrentDomain.FriendlyName;

        public static void Publish(string name, string payload)
        {
            TaskCompletionSource wake;
            lock (Gate)
            {
                Log.Add(new XruiosEvent(++_seq, Source, name, payload, DateTimeOffset.Now));
                if (Log.Count > Capacity) Log.RemoveRange(0, Log.Count - Capacity);
                wake = _changed;
                _changed = NewSignal();
            }
            wake.TrySetResult();
        }

        public static void Publish<T>(string name, T payload) => Publish(name, XRUIOS.Interpreter.EventJson.Write(payload));

        /// <summary>Events after <paramref name="after"/>, waiting up to <paramref name="waitMs"/> for the first one.</summary>
        public static async Task<XruiosEventBatch> PollAsync(string? epoch, long after, int waitMs)
        {
            if (epoch != Epoch) after = 0;   // new process: the caller's cursor belongs to the old log
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Clamp(waitMs, 0, XruiosEventProtocol.MaxWaitMs));

            while (true)
            {
                Task wait;
                lock (Gate)
                {
                    long oldest = Log.Count > 0 ? Log[0].Seq : _seq + 1;
                    var ready = Log.Where(e => e.Seq > after).ToList();
                    if (ready.Count > 0 || DateTime.UtcNow >= deadline)
                        return new XruiosEventBatch(_seq, after > 0 && after < oldest - 1, ready, Epoch);
                    wait = _changed.Task;
                }
                var left = deadline - DateTime.UtcNow;
                if (left > TimeSpan.Zero) await Task.WhenAny(wait, Task.Delay(left));
            }
        }

        private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
