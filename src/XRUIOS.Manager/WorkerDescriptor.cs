namespace XRUIOS.Manager
{
    /// <summary>
    /// Static config for one worker the Manager is responsible for. In production this list comes
    /// from the Manager's install manifest; here it's built in Program.cs pointing at the compiled
    /// Plagues worker exes.
    /// </summary>
    public sealed class WorkerDescriptor
    {
        /// <summary>Logical name — must match the SecureStore key the worker publishes under.</summary>
        public required string Name { get; init; }

        /// <summary>Path to the worker executable the Manager launches.</summary>
        public required string ExecutablePath { get; init; }

        /// <summary>
        /// Folder the Manager checksums before launch. Defaults to the executable's directory.
        /// </summary>
        public string Folder => Path.GetDirectoryName(Path.GetFullPath(ExecutablePath))!;
    }
}
