using EclipseProject;
using XRUIOS.Interfaces;
using XRUIOS.Interpreter;

namespace XRUIOS.Worker.SystemInfo
{
    // Power + machine facts. PowerRequest/BatteryStatus are what the PowerControl hole calls.
    public static class SystemInfoCapabilities
    {
        [SeaOfDirac("SystemInfo.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.SystemInfo: " + input;

        /// <summary>action = Shutdown | Restart | Sleep | Hibernate | SignOut | Lock.</summary>
        /// <remarks>
        /// PowerRequested is announced first, and for actions that end the session the machine waits a
        /// moment so hosts listening for it (save state, fade out) hear it before everything stops.
        /// </remarks>
        [SeaOfDirac("PowerRequest", new[] { "action" }, typeof(string), typeof(string))]
        public static async Task<string> PowerRequest(string action)
        {
            var a = Wire.ParsePower(action);
            WorkerEvents.Publish(XruiosEventNames.PowerRequested, EventJson.Write(new { action = a.ToString() }));
            if (a is PowerAction.Shutdown or PowerAction.Restart or PowerAction.SignOut)
                await Task.Delay(TimeSpan.FromSeconds(2));
            XruiosInterpreter.Current.Power.Request(a);
            return a.ToString();
        }

        /// <summary>"percent|charging|pluggedIn|minutesLeft" (percent / minutes are -1 when unknown).</summary>
        [SeaOfDirac("BatteryStatus", new string[0], typeof(string))]
        public static string BatteryStatus() => Wire.Battery(XruiosInterpreter.Current.Power.GetBattery());

        /// <summary>
        /// Cross-platform specs, one "Key: value" per line. (SystemInfoDisplayWindows.GenerateSpecs is
        /// the older WMI-only version; this works on Linux too.)
        /// </summary>
        [SeaOfDirac("SystemSpecs", new string[0], typeof(string))]
        public static string SystemSpecs() => Wire.Specs(XruiosInterpreter.Current.SystemInfo.GetSpecs());
    }
}
