namespace XRUIOS.Interfaces
{
    // The seam where XRUIOS.Manager / XRUIOS.Permission plugs in.
    //
    // Every gated call arrives at the worker carrying the caller's identity (the
    // random UUID minted during the Eclipse handshake = the enrolled clientId) and
    // the capability it wants. The Manager decides yes/no per app at the broker; the
    // worker's own gate is the second wall: it only serves sessions the Manager opened.
    public interface IPermissionGate
    {
        /// <summary>
        /// Return true to allow the capability, false to refuse ("Fuck you").
        /// </summary>
        /// <param name="requesterUuid">The caller's Eclipse identity (its UUID).</param>
        /// <param name="capability">The [SeaOfDirac] capability name being requested.</param>
        bool Check(string requesterUuid, string capability);
    }

    /// <summary>
    /// The default worker gate. A call is served only on a session whose enrollment carried a valid
    /// Manager signature (see <see cref="SecureWorkerHost"/>), so a peer that somehow got the worker's
    /// PSK but not the Manager's private identity key still gets nothing. Per-app decisions stay at the
    /// broker; this gate answers "is this the Manager?" and nothing else.
    /// </summary>
    public sealed class ManagerOnlyGate : IPermissionGate
    {
        public bool Check(string requesterUuid, string capability) =>
            SecureWorkerHost.IsVerifiedManagerSession(requesterUuid);
    }

    /// <summary>Allows everything. Only for throwaway tests; never hand this to a worker.</summary>
    [Obsolete("Allows any caller. Workers use ManagerOnlyGate (the default when no gate is passed).")]
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
