namespace XRUIOS.Interfaces
{
    /// <summary>
    /// What a worker publishes about itself at startup so the XRUIOS.Manager can build its
    /// registry: where to reach it, what it can do, and its PUBLIC key. The matching private
    /// key is generated in-process and never leaves the worker — the Manager only ever sees this.
    /// </summary>
    public sealed class WorkerAnnouncement
    {
        /// <summary>SecureStore suffix appended to the worker name for the announcement record.</summary>
        public const string StoreSuffix = ".announce";

        /// <summary>Logical worker name (matches its SecureStore key).</summary>
        public string Name { get; set; } = "";

        /// <summary>Bound loopback address clients/the Manager connect to.</summary>
        public string Address { get; set; } = "";

        /// <summary>The worker's Kyber PUBLIC key (private half stays in the worker).</summary>
        public Dictionary<string, byte[]> PublicKey { get; set; } = new();

        /// <summary>The [SeaOfDirac] capability names this worker exposes.</summary>
        public string[] Capabilities { get; set; } = Array.Empty<string>();

        /// <summary>Process id, so the Manager can supervise/stop it.</summary>
        public int ProcessId { get; set; }

        public static string StoreKey(string workerName) => workerName + StoreSuffix;
    }
}
