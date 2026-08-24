namespace XRUIOS.Interfaces
{
    // The seam where XRUIOS.Manager / XRUIOS.Permission plugs in.
    //
    // Every gated call arrives at the worker carrying the caller's identity (the
    // random UUID minted during the Eclipse handshake = the enrolled clientId) and
    // the capability it wants. The Manager decides yes/no; the worker only enforces
    // the answer. In this repo we ship safe stubs so the workers run standalone;
    // the real gate lives in the Manager and consults XRUIOS.Permission.
    public interface IPermissionGate
    {
        /// <summary>
        /// Return true to allow the capability, false to refuse ("Fuck you").
        /// </summary>
        /// <param name="requesterUuid">The caller's Eclipse identity (its UUID).</param>
        /// <param name="capability">The [SeaOfDirac] capability name being requested.</param>
        bool Check(string requesterUuid, string capability);
    }

    /// <summary>Default stub: allows everything. Replace with the Manager's gate.</summary>
    public sealed class AllowAllPermissionGate : IPermissionGate
    {
        public bool Check(string requesterUuid, string capability) => true;
    }

    /// <summary>
    /// Simple allow-list gate: only the named capabilities are permitted, everything
    /// else is refused. Handy for demos and for workers that expose a fixed surface.
    /// </summary>
    public sealed class CapabilityAllowListGate : IPermissionGate
    {
        private readonly HashSet<string> _allowed;

        public CapabilityAllowListGate(params string[] allowedCapabilities)
        {
            _allowed = new HashSet<string>(allowedCapabilities, StringComparer.Ordinal);
        }

        public bool Check(string requesterUuid, string capability) => _allowed.Contains(capability);
    }
}
