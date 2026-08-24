using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.DirectoryServices.AccountManagement;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;

// Hardened OS authentication for the identity class.
//
// This is the secure replacement for the older PowerShell/WMI/folder-scraping account code that
// lives in IdentityClass.cs (ValidateCredentials, GetUserAccountsWindows, ConnectAccToUser). It uses
// the platform's REAL authentication mechanism — LogonUser + PrincipalContext on Windows, PAM +
// getpwent on Linux — with no shelled-out commands (so no injection surface) and no default-allow.
//
// Everything here is PRIVATE to UserManager (nested types + private helpers) so nothing new leaks
// into the public surface. Call GetRealLocalAccounts / BindToCurrentUser / AuthenticateOsLogin from
// the existing methods as you migrate off the PowerShell versions.

#nullable disable

namespace XRUIOS.Barebones.Functions
{
    public partial class UserManager
    {
        // One provider for the process, created lazily for the current OS.
        private IAuthProvider _authProvider;
        private IAuthProvider Auth => _authProvider ??= AuthProviderFactory.Create();

        // Real local accounts (SAM via PrincipalContext / getpwent) — supersedes the folder listing
        // in GetUserAccountsWindows().
        private IReadOnlyList<string> GetRealLocalAccounts() => Auth.GetLocalAccounts();

        // How many real local OS accounts exist. CreateUserAsync uses this to make the FIRST user on a
        // fresh machine an administrator and everyone after a standard user. One source of truth: it
        // wraps GetRealLocalAccounts so the count always matches the enumeration.
        private int GetLocalUserCount() => GetRealLocalAccounts().Count;

        // No-password identity confirmation: is `username` the OS-authenticated owner of THIS process?
        // The common, unprivileged path — use this for the desktop/companion build.
        private bool BindToCurrentUser(string username) => Auth.BindCurrentUser(username).Success;

        // Full OS logon (LogonUser / PAM). Boot-time / kiosk greeter path only — needs privilege on
        // Linux (reads /etc/shadow via PAM). Supersedes the PowerShell ValidateCredentials(). The
        // provider zeroes the password before returning. Keep this SEPARATE from BindToCurrentUser —
        // the two are different trust levels on purpose.
        private bool AuthenticateOsLogin(string username, SecureString password)
            => Auth.LoginWithCredentials(username, password).Success;

        // ─────────────────────────────────────────────────────────────────────────────────────────
        //  Below: the provider abstraction, nested and private to UserManager.
        // ─────────────────────────────────────────────────────────────────────────────────────────

        private enum AuthFailureReason
        {
            None,
            InvalidCredentials,   // covers "no such user" AND "wrong password" — never distinguished, to block enumeration
            NotPermitted,
            ProviderError
        }

        private sealed class AuthResult
        {
            public bool Success { get; }
            public string Username { get; }
            public AuthFailureReason FailureReason { get; }

            private AuthResult(bool success, string username, AuthFailureReason reason)
            {
                Success = success;
                Username = username;
                FailureReason = reason;
            }

            public static AuthResult Ok(string username) => new AuthResult(true, username, AuthFailureReason.None);
            public static AuthResult Fail(AuthFailureReason reason) => new AuthResult(false, null, reason);
        }

        private interface IAuthProvider : IDisposable
        {
            IReadOnlyList<string> GetLocalAccounts();
            string GetCurrentUser();
            AuthResult BindCurrentUser(string requestedUsername);
            AuthResult LoginWithCredentials(string username, SecureString password);
        }

        private static class AuthProviderFactory
        {
            public static IAuthProvider Create()
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return new WindowsAuthProvider();
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return new LinuxAuthProvider();
                throw new PlatformNotSupportedException("XRUIOS.Auth has no provider for this platform yet.");
            }
        }

        // ---- Windows: LogonUser + PrincipalContext ----

        private sealed class WindowsAuthProvider : IAuthProvider
        {
            private const int LOGON32_LOGON_INTERACTIVE = 2;
            private const int LOGON32_PROVIDER_DEFAULT = 0;

            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            private static extern bool LogonUser(
                string lpszUsername, string lpszDomain, IntPtr lpszPassword,
                int dwLogonType, int dwLogonProvider, out IntPtr phToken);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool CloseHandle(IntPtr hObject);

            public IReadOnlyList<string> GetLocalAccounts()
            {
                var accounts = new List<string>();
                using (var context = new PrincipalContext(ContextType.Machine))
                using (var searcher = new UserPrincipal(context))
                using (var pse = new PrincipalSearcher(searcher))
                {
                    foreach (var result in pse.FindAll())
                    {
                        if (!string.IsNullOrEmpty(result.SamAccountName))
                            accounts.Add(result.SamAccountName);
                        result.Dispose();
                    }
                }
                return accounts;
            }

            public string GetCurrentUser()
            {
                using (var identity = WindowsIdentity.GetCurrent())
                    return StripDomain(identity.Name);
            }

            public AuthResult BindCurrentUser(string requestedUsername)
            {
                if (string.IsNullOrWhiteSpace(requestedUsername))
                    return AuthResult.Fail(AuthFailureReason.NotPermitted);

                var current = GetCurrentUser();
                if (!string.Equals(current, StripDomain(requestedUsername), StringComparison.OrdinalIgnoreCase))
                    return AuthResult.Fail(AuthFailureReason.NotPermitted);

                return AuthResult.Ok(current);
            }

            public AuthResult LoginWithCredentials(string username, SecureString password)
            {
                if (string.IsNullOrWhiteSpace(username) || password == null)
                    return AuthResult.Fail(AuthFailureReason.InvalidCredentials);

                IntPtr passwordPtr = IntPtr.Zero;
                IntPtr token = IntPtr.Zero;
                try
                {
                    passwordPtr = Marshal.SecureStringToGlobalAllocUnicode(password);

                    bool ok = LogonUser(
                        username,
                        lpszDomain: ".",            // local machine account
                        lpszPassword: passwordPtr,
                        dwLogonType: LOGON32_LOGON_INTERACTIVE,
                        dwLogonProvider: LOGON32_PROVIDER_DEFAULT,
                        phToken: out token);

                    if (!ok)
                    {
                        _ = Marshal.GetLastWin32Error(); // never branch on the specific error → no username enumeration
                        return AuthResult.Fail(AuthFailureReason.InvalidCredentials);
                    }

                    return AuthResult.Ok(username);
                }
                catch (Win32Exception)
                {
                    return AuthResult.Fail(AuthFailureReason.ProviderError);
                }
                finally
                {
                    if (passwordPtr != IntPtr.Zero)
                        Marshal.ZeroFreeGlobalAllocUnicode(passwordPtr); // zero, not just free
                    if (token != IntPtr.Zero)
                        CloseHandle(token);
                    password?.Dispose();
                }
            }

            private static string StripDomain(string accountName)
            {
                if (string.IsNullOrEmpty(accountName)) return accountName;
                int slash = accountName.IndexOf('\\');
                return slash >= 0 ? accountName.Substring(slash + 1) : accountName;
            }

            public void Dispose() { }
        }

        // ---- Linux: PAM + getpwent ----

        private sealed class LinuxAuthProvider : IAuthProvider
        {
            private const string PamService = "xruios-login"; // ship /etc/pam.d/xruios-login

            [StructLayout(LayoutKind.Sequential)]
            private struct PamMessage { public int msg_style; public IntPtr msg; }

            [StructLayout(LayoutKind.Sequential)]
            private struct PamResponse { public IntPtr resp; public int resp_retcode; }

            private delegate int PamConvDelegate(int num_msg, IntPtr msg, out IntPtr resp, IntPtr appdata_ptr);

            [StructLayout(LayoutKind.Sequential)]
            private struct PamConv { public PamConvDelegate conv; public IntPtr appdata_ptr; }

            private const int PAM_PROMPT_ECHO_OFF = 1;
            private const int PAM_SUCCESS = 0;

            [DllImport("libpam.so.0")]
            private static extern int pam_start(string service_name, string user, ref PamConv pam_conversation, out IntPtr pamh);
            [DllImport("libpam.so.0")]
            private static extern int pam_authenticate(IntPtr pamh, int flags);
            [DllImport("libpam.so.0")]
            private static extern int pam_acct_mgmt(IntPtr pamh, int flags);
            [DllImport("libpam.so.0")]
            private static extern int pam_end(IntPtr pamh, int pam_status);
            [DllImport("libpam.so.0")]
            private static extern IntPtr pam_strerror(IntPtr pamh, int errnum);

            [StructLayout(LayoutKind.Sequential)]
            private struct Passwd
            {
                public IntPtr pw_name; public IntPtr pw_passwd; public uint pw_uid; public uint pw_gid;
                public IntPtr pw_gecos; public IntPtr pw_dir; public IntPtr pw_shell;
            }

            [DllImport("libc")] private static extern void setpwent();
            [DllImport("libc")] private static extern IntPtr getpwent();
            [DllImport("libc")] private static extern void endpwent();
            [DllImport("libc")] private static extern uint geteuid();
            [DllImport("libc")] private static extern IntPtr getpwuid(uint uid);

            // holds the password for the lifetime of a single LoginWithCredentials call, cleared in finally
            private IntPtr _pendingPasswordPtr = IntPtr.Zero;

            public IReadOnlyList<string> GetLocalAccounts()
            {
                var accounts = new List<string>();
                setpwent();
                try
                {
                    IntPtr entry;
                    while ((entry = getpwent()) != IntPtr.Zero)
                    {
                        var pw = Marshal.PtrToStructure<Passwd>(entry);
                        if (pw.pw_uid >= 1000) // skip system/service accounts (common distro floor)
                        {
                            string name = Marshal.PtrToStringAnsi(pw.pw_name);
                            if (!string.IsNullOrEmpty(name))
                                accounts.Add(name);
                        }
                    }
                }
                finally { endpwent(); }
                return accounts;
            }

            public string GetCurrentUser()
            {
                IntPtr pw = getpwuid(geteuid());
                if (pw == IntPtr.Zero) return null;
                var entry = Marshal.PtrToStructure<Passwd>(pw);
                return Marshal.PtrToStringAnsi(entry.pw_name);
            }

            public AuthResult BindCurrentUser(string requestedUsername)
            {
                if (string.IsNullOrWhiteSpace(requestedUsername))
                    return AuthResult.Fail(AuthFailureReason.NotPermitted);

                var current = GetCurrentUser();
                if (!string.Equals(current, requestedUsername, StringComparison.Ordinal))
                    return AuthResult.Fail(AuthFailureReason.NotPermitted);

                return AuthResult.Ok(current);
            }

            public AuthResult LoginWithCredentials(string username, SecureString password)
            {
                if (string.IsNullOrWhiteSpace(username) || password == null)
                    return AuthResult.Fail(AuthFailureReason.InvalidCredentials);

                IntPtr pamh = IntPtr.Zero;
                _pendingPasswordPtr = Marshal.SecureStringToGlobalAllocAnsi(password);

                PamConvDelegate convCallback = PamConversationCallback;
                var conv = new PamConv { conv = convCallback, appdata_ptr = IntPtr.Zero };

                try
                {
                    int rc = pam_start(PamService, username, ref conv, out pamh);
                    if (rc != PAM_SUCCESS)
                        return AuthResult.Fail(AuthFailureReason.ProviderError);

                    rc = pam_authenticate(pamh, 0);
                    if (rc != PAM_SUCCESS)
                        return AuthResult.Fail(AuthFailureReason.InvalidCredentials);

                    rc = pam_acct_mgmt(pamh, 0);
                    if (rc != PAM_SUCCESS)
                        return AuthResult.Fail(AuthFailureReason.InvalidCredentials);

                    return AuthResult.Ok(username);
                }
                finally
                {
                    if (pamh != IntPtr.Zero) pam_end(pamh, 0);
                    if (_pendingPasswordPtr != IntPtr.Zero)
                    {
                        Marshal.ZeroFreeGlobalAllocAnsi(_pendingPasswordPtr);
                        _pendingPasswordPtr = IntPtr.Zero;
                    }
                    password?.Dispose();
                }
            }

            private int PamConversationCallback(int num_msg, IntPtr msgPtr, out IntPtr respPtr, IntPtr appdata_ptr)
            {
                respPtr = IntPtr.Zero;
                var responses = new PamResponse[num_msg];

                for (int i = 0; i < num_msg; i++)
                {
                    IntPtr msgStructPtr = Marshal.ReadIntPtr(msgPtr, i * Marshal.SizeOf<IntPtr>());
                    var msg = Marshal.PtrToStructure<PamMessage>(msgStructPtr);

                    if (msg.msg_style == PAM_PROMPT_ECHO_OFF)
                    {
                        responses[i].resp = _pendingPasswordPtr == IntPtr.Zero
                            ? IntPtr.Zero
                            : Marshal.PtrToStringAnsi(_pendingPasswordPtr) is string s
                                ? Marshal.StringToHGlobalAnsi(s) // PAM takes ownership and frees this itself
                                : IntPtr.Zero;
                    }
                    else
                    {
                        responses[i].resp = IntPtr.Zero;
                    }
                    responses[i].resp_retcode = 0;
                }

                int size = Marshal.SizeOf<PamResponse>();
                respPtr = Marshal.AllocHGlobal(size * num_msg);
                for (int i = 0; i < num_msg; i++)
                    Marshal.StructureToPtr(responses[i], respPtr + i * size, false);

                return PAM_SUCCESS;
            }

            public void Dispose() { }
        }
    }
}
