namespace XRUIOS.Interfaces
{
    /// <summary>
    /// Facts about the call a capability is currently serving. The Manager stamps the calling app on
    /// every relayed request, so a capability can record who asked (the assistant keeps it with each
    /// question) without trusting anything the app sent.
    /// </summary>
    public static class WorkerCall
    {
        internal static readonly AsyncLocal<string?> CallerSlot = new();

        /// <summary>The appId of the app that made this call ("app:..."), or null outside a relayed call.</summary>
        public static string? CallerAppId => CallerSlot.Value;
    }
}
