namespace XRUIOS.Interfaces
{
    /// <summary>
    /// The credentials the Manager issues to an app so it can reach the broker: its stable id, its
    /// per-app password, and where the broker listens. The Manager makes the password fresh every time it
    /// starts and never writes it to disk. An app receives it in one of two ways: in its environment when
    /// the Manager launches it, or by enrolling over the control pipe (see <see cref="XruiosEnrollment"/>),
    /// where the Manager first checks which program is asking and that its folder is untampered.
    /// </summary>
    public sealed class AppProvisioning
    {
        public string AppId { get; set; } = "";
        public string PskBase64 { get; set; } = "";
        public string BrokerAddress { get; set; } = "";

        public byte[] Psk => Convert.FromBase64String(PskBase64);
    }
}
