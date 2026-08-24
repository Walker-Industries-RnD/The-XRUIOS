using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace XRUIOS.Interfaces
{
    public class Utils
    {
        public static class SecureStore
        {
            private static string BasePath
            {
                get
                {
                    // Use per-session temp directory on all platforms
                    string sessionDir = Path.Combine(Path.GetTempPath(), "SECURE_STORE" + Environment.UserName);
                    Directory.CreateDirectory(sessionDir);
                    return sessionDir;
                }
            }

            private static string GetPath(string key) =>
                Path.Combine(BasePath, $"secstr_{key}.dat");

            private static void EnsureFolder()
            {
                if (!Directory.Exists(BasePath))
                {
                    Directory.CreateDirectory(BasePath);

                    if (OperatingSystem.IsWindows())
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(BasePath);
                            var dirSecurity = dirInfo.GetAccessControl();

                            // Remove inheritance & keep only explicit rules
                            dirSecurity.SetAccessRuleProtection(true, false);

                            // Give full control to current user only
                            var currentUser = WindowsIdentity.GetCurrent().User!;
                            dirSecurity.AddAccessRule(new FileSystemAccessRule(
                                currentUser,
                                FileSystemRights.FullControl,
                                AccessControlType.Allow
                            ));

                            dirInfo.SetAccessControl(dirSecurity);

                            // Hide the folder from casual browsing
                            dirInfo.Attributes |= FileAttributes.Hidden | FileAttributes.System;
                        }
                        catch
                        {
                            // If ACLs fail, fall back silently; still writable
                        }
                    }
                }
            }

            public static void Set<T>(string key, T value)
            {
                EnsureFolder();

                string path = GetPath(key);
                string json = JsonSerializer.Serialize(value);
                WriteWithRetry(path, json);

                if (!OperatingSystem.IsWindows())
                {
                    ApplyUnixPermissions(path);
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

            private static void ApplyUnixPermissions(string path)
            {
                try
                {
                    var chmod = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/bin/chmod",
                        Arguments = $"600 \"{path}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = System.Diagnostics.Process.Start(chmod);
                    proc?.WaitForExit();
                }
                catch
                {
                    // fallback: hide file (less secure)
                    File.SetAttributes(path, FileAttributes.Hidden);
                }
            }


        }

    }
}
