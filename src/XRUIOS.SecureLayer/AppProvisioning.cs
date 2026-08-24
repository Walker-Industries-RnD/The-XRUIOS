namespace XRUIOS.Interfaces
{
    /// <summary>
    /// The credentials the Manager issues to an app so it can reach the broker: its stable id, its
    /// per-app password, and where the broker listens. In production these are delivered to the app
    /// at first verification; for the demo the Manager drops them in SecureStore under
    /// <see cref="StoreKey"/> and the app reads them back (same-user handoff).
    /// </summary>
    public sealed class AppProvisioning
    {
        public string AppId { get; set; } = "";
        public string PskBase64 { get; set; } = "";
        public string BrokerAddress { get; set; } = "";

        public byte[] Psk => Convert.FromBase64String(PskBase64);

        public static string StoreKey(string appName) => "xruios.app." + appName;
    }
}
