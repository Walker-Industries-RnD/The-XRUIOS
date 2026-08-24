namespace XRUIOS.Contracts
{
    /// <summary>
    /// The seven top-level permission groups. Every XRUIOS system class belongs to exactly one, and
    /// every worker inherits its class's group. Grants are per-capability, but the group is the coarse
    /// bucket used for the permissions catalog and default policy.
    /// </summary>
    public enum PermissionGroup
    {
        Media,
        Time,
        Spatial,
        Audio,
        Interface,
        Identity,
        System
    }
}
