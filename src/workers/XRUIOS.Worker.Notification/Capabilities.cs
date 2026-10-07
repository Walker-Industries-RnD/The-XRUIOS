using System.Text;
using EclipseProject;
using XRUIOS.Interfaces;
using XRUIOS.Barebones.Functions;
using XRUIOS.Interpreter;
using static XRUIOS.Barebones.Interfaces.NotificationClass;

namespace XRUIOS.Worker.Notification
{
    // NotificationClass keeps the history (what the Notifications app lists). Every post is announced
    // as NotificationPosted so a host can present it its own way (an in-world panel, a sound...); the
    // OS toast is on by default and a host that draws its own turns it off with SetOsNotifications.
    // Unread/Dismiss are what the NotificationFeed hole calls; Notify is how any granted program posts.
    public static class NotificationCapabilities
    {
        [SeaOfDirac("Notification.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.Notification: " + input;

        /// <summary>"id|source|title|body|timestamp;..." for every live (unexpired) notification.</summary>
        [SeaOfDirac("Unread", new string[0], typeof(string))]
        public static async Task<string> Unread()
        {
            var all = await NotificationClass.GetNotifications();
            return string.Join(';', all.Select(n =>
            {
                var texts = n.Texts ?? new List<string>();
                string title = texts.FirstOrDefault() ?? "";
                string body = string.Join(" ", texts.Skip(1));
                string source = n.Group ?? n.Action ?? "";
                string when = n.Args != null && n.Args.TryGetValue("postedAt", out var at) ? at : "";
                return $"{IdFor(n)}|{Wire.Field(source)}|{Wire.Field(title)}|{Wire.Field(body)}|{Wire.Field(when)}";
            }));
        }

        [SeaOfDirac("Dismiss", new[] { "id" }, typeof(string), typeof(string))]
        public static async Task<string> Dismiss(string id)
        {
            var (group, tag) = Decode(id);
            await NotificationClass.RemoveNotification(tag, group);
            WorkerEvents.Publish(XruiosEventNames.NotificationDismissed, EventJson.Write(new { id }));
            return "dismissed";
        }

        /// <summary>
        /// Post a notification: stored in the history, announced as NotificationPosted, and shown as an
        /// OS toast unless that's switched off. If the toast can't be shown (no notification daemon,
        /// focus assist...), the notification is still kept and announced.
        /// </summary>
        [SeaOfDirac("Notify", new[] { "title", "body", "source" }, typeof(string), typeof(string), typeof(string), typeof(string))]
        public static async Task<string> Notify(string title, string body, string source)
        {
            string group = string.IsNullOrWhiteSpace(source) ? "XRUIOS" : source;
            var n = new NotificationContent(
                action: "xruios",
                args: new Dictionary<string, string> { ["postedAt"] = DateTimeOffset.Now.ToString("O") },
                texts: new List<string> { title, body },
                tag: Guid.NewGuid().ToString("N"),
                group: group);
            await NotificationClass.AddNotification(n);

            string id = IdFor(n);
            WorkerEvents.Publish(XruiosEventNames.NotificationPosted,
                EventJson.Write(new NotificationInfo(id, group, title, body, DateTimeOffset.Now)));

            if (!OsToastsEnabled()) return id;
            try { XruiosInterpreter.Current.Notifications.Show(title, body, group); }
            catch (Exception ex) { return $"{id}|stored, not shown: {Wire.Field(ex.Message)}"; }
            return id;
        }

        /// <summary>Show posts as OS notifications (default on). Hosts that present them themselves turn this off.</summary>
        [SeaOfDirac("SetOsNotifications", new[] { "enabled" }, typeof(string), typeof(bool))]
        public static async Task<string> SetOsNotifications(bool enabled)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OsToastFile)!);
            await File.WriteAllTextAsync(OsToastFile, enabled ? "on" : "off");
            return enabled ? "on" : "off";
        }

        private static string OsToastFile =>
            Path.Combine(global::XRUIOS.Barebones.XRUIOS.DataPath, "NotificationHistory", "os-toasts");

        private static bool OsToastsEnabled() =>
            !File.Exists(OsToastFile) || File.ReadAllText(OsToastFile).Trim() != "off";

        // Group/tag are free text, so the id is an opaque url-safe encoding of both.
        private static string IdFor(NotificationContent n) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{n.Group}\n{n.Tag}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static (string group, string tag) Decode(string id)
        {
            try
            {
                string b64 = id.Replace('-', '+').Replace('_', '/');
                b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
                var parts = Encoding.UTF8.GetString(Convert.FromBase64String(b64)).Split('\n', 2);
                if (parts.Length == 2) return (parts[0], parts[1]);
            }
            catch (FormatException) { /* fall through */ }
            throw new ArgumentException($"'{id}' is not a notification id.");
        }
    }
}
