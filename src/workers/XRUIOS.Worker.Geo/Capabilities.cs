using System.Text.Json.Nodes;
using EclipseProject;
using XRUIOS.Interfaces;
using XRUIOS.Interpreter;
using static Pariah_Cybersecurity.DataHandler;

namespace XRUIOS.Worker.Geo
{
    // Location, plus the latest weather. The XRUIOS doesn't choose a weather service: a provider app
    // (an IWeatherSource driven by WeatherProvider) pushes snapshots in with SetWeather, each one is
    // announced as WeatherUpdated, and every app reads it with Current (the WeatherFeed hole).
    public static class GeoCapabilities
    {
        private static string WeatherDir => Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "Weather");

        [SeaOfDirac("Geo.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Geo: " + input;

        /// <summary>
        /// "location|tempC|condition|high|low" of the latest snapshot, or "" if no provider has pushed
        /// one yet. <paramref name="city"/> is passed through for providers that serve several places;
        /// the stored snapshot is for whatever location the provider last reported.
        /// </summary>
        [SeaOfDirac("Current", new[] { "city" }, typeof(string), typeof(string))]
        public static async Task<string> Current(string city)
        {
            var w = await LoadAsync();
            return w is null ? "" : $"{Wire.Field(w.Location)}|{w.TempC}|{Wire.Field(w.Condition)}|{w.High}|{w.Low}";
        }

        [SeaOfDirac("SetWeather", new[] { "location", "tempC", "condition", "high", "low" },
            typeof(string), typeof(string), typeof(long), typeof(string), typeof(long), typeof(long))]
        public static async Task<string> SetWeather(string location, long tempC, string condition, long high, long low)
        {
            var snap = new WeatherSnapshot(location, (int)tempC, condition, (int)high, (int)low, DateTimeOffset.Now);
            string json = EventJson.Write(snap);

            Directory.CreateDirectory(WeatherDir);
            if (!File.Exists(Path.Combine(WeatherDir, "Weather.json")))
                await JSONDataHandler.CreateJsonFile("Weather", WeatherDir, new JsonObject());
            var store = await JSONDataHandler.LoadJsonFile("Weather", WeatherDir);
            store = await JSONDataHandler.CheckIfVariableExists(store, "Data")
                ? await JSONDataHandler.UpdateJson<string>(store, "Data", json, global::XRUIOS.Barebones.XRUIOS.encryptionKey)
                : await JSONDataHandler.AddToJson<string>(store, "Data", json, global::XRUIOS.Barebones.XRUIOS.encryptionKey);
            await JSONDataHandler.SaveJson(store);

            WorkerEvents.Publish(XruiosEventNames.WeatherUpdated, json);
            return "ok";
        }

        private static async Task<WeatherSnapshot?> LoadAsync()
        {
            if (!File.Exists(Path.Combine(WeatherDir, "Weather.json"))) return null;
            var store = await JSONDataHandler.LoadJsonFile("Weather", WeatherDir);
            if (!await JSONDataHandler.CheckIfVariableExists(store, "Data")) return null;
            var json = (string)await JSONDataHandler.GetVariable<string>(store, "Data", global::XRUIOS.Barebones.XRUIOS.encryptionKey);
            return string.IsNullOrEmpty(json) ? null : EventJson.Read<WeatherSnapshot>(json);
        }
    }
}
