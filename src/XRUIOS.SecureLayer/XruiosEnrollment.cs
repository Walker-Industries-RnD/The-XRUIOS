using System.IO.Pipes;
using System.Text;

namespace XRUIOS.Interfaces
{
    /// <summary>
    /// Self-service enrollment for a STANDALONE app (its own exe). Instead of being spawned by the
    /// Manager with credentials in its environment, the app asks the running Manager for them over a
    /// local control pipe. The Manager attests the CALLING process — it verifies the connecting binary
    /// is the registered app — before returning anything, so there is still no secret at rest and the
    /// app doesn't need a parent to hand it a key.
    /// </summary>
    public static class XruiosEnrollment
    {
        // Must match XRUIOS.Manager.AppLauncher.PipeName.
        public const string PipeName = "xruios-manager-control";

        /// <summary>
        /// Ask the Manager to enroll this process as <paramref name="appName"/>. Throws if no Manager is
        /// running or attestation is refused (wrong/tampered binary, unknown app).
        /// </summary>
        public static async Task<AppProvisioning> EnrollAsync(string appName, int timeoutMs = 3000)
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            try { await client.ConnectAsync(timeoutMs); }
            catch (TimeoutException) { throw new Exception("No XRUIOS.Manager is running to enroll with."); }

            await client.WriteAsync(Encoding.UTF8.GetBytes("enroll " + appName));
            await client.FlushAsync();

            var buf = new byte[2048];
            int n = await client.ReadAsync(buf);
            string reply = Encoding.UTF8.GetString(buf, 0, n);

            // Wire format:  OK \t <appId> \t <pskBase64> \t <brokerAddr>   |   DENY \t <reason>
            var parts = reply.Split('\t');
            if (parts.Length >= 4 && parts[0] == "OK")
                return new AppProvisioning { AppId = parts[1], PskBase64 = parts[2], BrokerAddress = parts[3] };

            throw new Exception("Manager refused enrollment: " + (parts.Length > 1 ? parts[1] : reply));
        }
    }
}
