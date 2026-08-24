using static Secure_Store.Storage;

namespace XRUIOS.Barebones
{
    public class ClipboardClass
    {
        public class BaseClipboard
        {
            private static Dictionary<string, byte[]> Clipboard = new Dictionary<string, byte[]>();

            // R
            public Dictionary<string, byte[]> LoadClipboard()
            {
                return Clipboard;
            }

            public byte[] GetClipboardItem(string key)
            {
                if (Clipboard.TryGetValue(key, out var value))
                    return value;

                // fallback to secure store
                return SecureStore.Get<byte[]>(key);
            }

            // U
            public void AddToClipboard(byte[] item, string key)
            {
                Clipboard[key] = item;
                SecureStore.Set(key, item);
            }

            // D
            public void RemoveFromClipboard(string key)
            {
                Clipboard.Remove(key);

                string path = GetPath(key);
                if (File.Exists(path))
                    File.Delete(path);
            }

            private static string BasePath
            {
                get
                {
                    string sessionDir = Path.Combine(Path.GetTempPath(), "SECURE_STORE_" + Environment.UserName);
                    Directory.CreateDirectory(sessionDir);
                    return sessionDir;
                }
            }

            private static string GetPath(string key) =>
                Path.Combine(BasePath, $"secstr_{key}.dat");
        }

        public class ClipboardGroups
        {
            private static Dictionary<string, Dictionary<string, byte[]>> ClipboardGroup = new();

            // R
            public Dictionary<string, byte[]> LoadClipboard(string groupName)
            {
                if (!ClipboardGroup.ContainsKey(groupName))
                    ClipboardGroup[groupName] = new Dictionary<string, byte[]>();

                return ClipboardGroup[groupName];
            }

            public async Task<byte[]> GetClipboardItem(string groupName, string key)
            {
                var group = LoadClipboard(groupName);
                if (group.TryGetValue(key, out var value))
                    return value;

                string path = GetPath(groupName, key);
                if (File.Exists(path))
                {
                    value = await File.ReadAllBytesAsync(path);
                    group[key] = value; // cache it
                    return value;
                }

                return null;
            }

            // U
            public void AddToClipboard(string groupName, byte[] item, string key)
            {
                var group = LoadClipboard(groupName);
                group[key] = item;

                string path = GetPath(groupName, key);
                File.WriteAllBytes(path, item);
            }

            // D
            public void RemoveFromClipboard(string groupName, string key)
            {
                var group = LoadClipboard(groupName);
                group.Remove(key);

                string path = GetPath(groupName, key);
                if (File.Exists(path))
                    File.Delete(path);
            }

            private static string BasePath
            {
                get
                {
                    string sessionDir = Path.Combine(Path.GetTempPath(), "SECURE_STORE_" + Environment.UserName);
                    Directory.CreateDirectory(sessionDir);
                    return sessionDir;
                }
            }

            private static string GetPath(string groupName, string key)
            {
                string groupDir = Path.Combine(BasePath, groupName);
                Directory.CreateDirectory(groupDir);
                return Path.Combine(groupDir, $"secstr_{key}.dat");
            }
        }
    }
}
