using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace XRUIOS.Interfaces
{
    public class Utils
    {
        public static class SecureStore
        {
            private static readonly object FolderLock = new();
            private static string? _basePath;

            // The folder is created and locked down once per process, before anything is written to it.
            private static string BasePath
            {
                get
                {
                    if (_basePath != null) return _basePath;
                    lock (FolderLock)
                    {
                        if (_basePath != null) return _basePath;
                        string dir = Path.Combine(Path.GetTempPath(), "SECURE_STORE" + Environment.UserName);
                        RestrictToCurrentUser(dir);
                        return _basePath = dir;
                    }
                }
            }

            private static string GetPath(string key) =>
                Path.Combine(BasePath, $"secstr_{key}.dat");

            // Windows: remove the inherited rules and allow only the current account, for the folder and
            // everything created in it. Unix: mode 700. Runs on an existing folder too, so a folder left by
            // an older build is tightened.
            private static void RestrictToCurrentUser(string dir)
            {
                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        Directory.CreateDirectory(dir);
                        var info = new DirectoryInfo(dir);
                        var security = info.GetAccessControl();
                        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                        security.AddAccessRule(new FileSystemAccessRule(
                            WindowsIdentity.GetCurrent().User!,
                            FileSystemRights.FullControl,
                            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                            PropagationFlags.None,
                            AccessControlType.Allow));
                        info.SetAccessControl(security);
                    }
                    else
                    {
                        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
                        Directory.CreateDirectory(dir, mode);
                        File.SetUnixFileMode(dir, mode);
                    }
                }
                catch (Exception ex)
                {
                    // The store holds addresses and public keys, no secrets, so the process keeps running; the
                    // folder then keeps whatever permissions the temp directory gave it.
                    Console.Error.WriteLine($"[SecureStore] Could not restrict '{dir}' to the current user: {ex.Message}");
                    Directory.CreateDirectory(dir);
                }
            }

            public static void Set<T>(string key, T value)
            {
                string path = GetPath(key);
                string json = JsonSerializer.Serialize(value);
                WriteWithRetry(path, json);

                if (!OperatingSystem.IsWindows())
                {
                    try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
                    catch { /* the folder is already 700 */ }
                }
            }

            public static T? Get<T>(string key)
            {
                string path = GetPath(key);
                if (!File.Exists(path))
                    return default;

                string? json = ReadWithRetry(path);
                if (json is null) return default;
                return JsonSerializer.Deserialize<T>(json);
            }

            // The store is a file per key, and the Manager reads a worker's announcement file while the
            // worker is writing it - under parallel worker launch that races. Retry on a sharing
            // violation instead of letting the IOException escape and take the process down.
            private static void WriteWithRetry(string path, string content)
            {
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    try { File.WriteAllText(path, content); return; }
                    catch (IOException) { System.Threading.Thread.Sleep(10); }
                }
            }

            private static string? ReadWithRetry(string path)
            {
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    try { return File.ReadAllText(path); }
                    catch (IOException) { System.Threading.Thread.Sleep(10); }
                }
                return null;
            }
        }
    }
}
