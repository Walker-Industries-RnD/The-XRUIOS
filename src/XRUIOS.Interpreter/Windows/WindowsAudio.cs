using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace XRUIOS.Interpreter.Windows
{
    /// <summary>
    /// Windows Core Audio: the default render endpoint is the master channel; every live audio session
    /// on it (one per app, grouped by process) is a per-app channel with id "pid:&lt;pid&gt;", or
    /// "system" for Windows' own system-sounds session.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class WindowsAudio : IAudioInterpreter
    {
        private static Guid _eventContext = Guid.NewGuid();

        public int GetMasterVolume() => WithEndpoint(ep =>
        {
            ep.GetMasterVolumeLevelScalar(out float level);
            return (int)Math.Round(level * 100);
        });

        public void SetMasterVolume(int volume0to100) => WithEndpoint(ep =>
        {
            ep.SetMasterVolumeLevelScalar(Shell.Clamp01to100(volume0to100) / 100f, ref _eventContext);
            return 0;
        });

        public bool GetMasterMute() => WithEndpoint(ep =>
        {
            ep.GetMute(out bool muted);
            return muted;
        });

        public void SetMasterMute(bool muted) => WithEndpoint(ep =>
        {
            ep.SetMute(muted, ref _eventContext);
            return 0;
        });

        public IReadOnlyList<AudioChannel> ListChannels()
        {
            var rows = new List<AudioChannel>
            {
                new(AudioChannel.MasterId, "Master", GetMasterVolume(), GetMasterMute()),
            };

            var seen = new HashSet<string>();
            foreach (var s in EnumerateSessions())
            {
                using (s)
                {
                    if (!seen.Add(s.ChannelId)) continue;
                    s.Volume.GetMasterVolume(out float level);
                    s.Volume.GetMute(out bool muted);
                    rows.Add(new AudioChannel(s.ChannelId, s.Name, (int)Math.Round(level * 100), muted));
                }
            }
            return rows;
        }

        public void SetChannelVolume(string channelId, int volume0to100)
        {
            if (channelId == AudioChannel.MasterId) { SetMasterVolume(volume0to100); return; }
            float level = Shell.Clamp01to100(volume0to100) / 100f;
            ForEachSession(channelId, s => s.Volume.SetMasterVolume(level, ref _eventContext));
        }

        public void SetChannelMute(string channelId, bool muted)
        {
            if (channelId == AudioChannel.MasterId) { SetMasterMute(muted); return; }
            ForEachSession(channelId, s => s.Volume.SetMute(muted, ref _eventContext));
        }

        // ── plumbing ─────────────────────────────────────────────────────────────────────────────

        private static T WithEndpoint<T>(Func<IAudioEndpointVolume, T> use)
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            IMMDevice? device = null;
            object? endpoint = null;
            try
            {
                enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out device);
                var iid = typeof(IAudioEndpointVolume).GUID;
                device.Activate(ref iid, ClsCtx.All, IntPtr.Zero, out endpoint);
                return use((IAudioEndpointVolume)endpoint);
            }
            finally
            {
                Release(endpoint);
                Release(device);
                Release(enumerator);
            }
        }

        private void ForEachSession(string channelId, Action<Session> act)
        {
            int hits = 0;
            foreach (var s in EnumerateSessions())
            {
                using (s)
                {
                    if (s.ChannelId != channelId) continue;
                    act(s);
                    hits++;
                }
            }
            if (hits == 0) throw new KeyNotFoundException($"No audio channel '{channelId}' is playing right now.");
        }

        private sealed class Session : IDisposable
        {
            public required string ChannelId { get; init; }
            public required string Name { get; init; }
            public required ISimpleAudioVolume Volume { get; init; }
            public required IAudioSessionControl2 Control { get; init; }

            public void Dispose()
            {
                Release(Volume);
                Release(Control);
            }
        }

        private static List<Session> EnumerateSessions()
        {
            var list = new List<Session>();
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            IMMDevice? device = null;
            object? managerObj = null;
            IAudioSessionEnumerator? sessions = null;
            try
            {
                enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out device);
                var iid = typeof(IAudioSessionManager2).GUID;
                device.Activate(ref iid, ClsCtx.All, IntPtr.Zero, out managerObj);
                ((IAudioSessionManager2)managerObj).GetSessionEnumerator(out sessions);
                sessions.GetCount(out int count);

                for (int i = 0; i < count; i++)
                {
                    sessions.GetSession(i, out IAudioSessionControl2 control);
                    control.GetState(out AudioSessionState state);
                    if (state == AudioSessionState.Expired) { Release(control); continue; }

                    bool isSystem = control.IsSystemSoundsSession() == 0;
                    control.GetProcessId(out uint pid);
                    control.GetDisplayName(out string? display);

                    list.Add(new Session
                    {
                        ChannelId = isSystem ? "system" : $"pid:{pid}",
                        Name = isSystem ? "System Sounds" : NameFor(pid, display),
                        Volume = (ISimpleAudioVolume)control,
                        Control = control,
                    });
                }
            }
            finally
            {
                Release(sessions);
                Release(managerObj);
                Release(device);
                Release(enumerator);
            }
            return list;
        }

        private static string NameFor(uint pid, string? display)
        {
            // Display names are often empty or an indirect "@%SystemRoot%\..." resource string.
            if (!string.IsNullOrWhiteSpace(display) && !display.StartsWith('@')) return display;
            try
            {
                using var p = Process.GetProcessById((int)pid);
                try
                {
                    var desc = p.MainModule?.FileVersionInfo.FileDescription;
                    if (!string.IsNullOrWhiteSpace(desc)) return desc;
                }
                catch { /* elevated / protected process: fall back to its name */ }
                return p.ProcessName;
            }
            catch { return $"Process {pid}"; }
        }

        private static void Release(object? com)
        {
            if (com != null && Marshal.IsComObject(com)) Marshal.ReleaseComObject(com);
        }

        // ── Core Audio COM surface (vtable order matters: every method up to the last used one) ──

        private enum EDataFlow { Render = 0, Capture = 1, All = 2 }
        private enum ERole { Console = 0, Multimedia = 1, Communications = 2 }
        private enum AudioSessionState { Inactive = 0, Active = 1, Expired = 2 }

        private static class ClsCtx { public const int All = 0x17; }

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            void EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IntPtr devices);
            void GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            void Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object anInterface);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            void RegisterControlChangeNotify(IntPtr notify);
            void UnregisterControlChangeNotify(IntPtr notify);
            void GetChannelCount(out uint count);
            void SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
            void SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
            void GetMasterVolumeLevel(out float levelDb);
            void GetMasterVolumeLevelScalar(out float level);
            void SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);
            void SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
            void GetChannelVolumeLevel(uint channel, out float levelDb);
            void GetChannelVolumeLevelScalar(uint channel, out float level);
            void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
            void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }

        [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionManager2
        {
            // IAudioSessionManager
            void GetAudioSessionControl(IntPtr sessionGuid, uint streamFlags, out IntPtr sessionControl);
            void GetSimpleAudioVolume(IntPtr sessionGuid, uint streamFlags, out IntPtr audioVolume);
            // IAudioSessionManager2
            void GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);
        }

        [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionEnumerator
        {
            void GetCount(out int sessionCount);
            void GetSession(int sessionIndex, out IAudioSessionControl2 session);
        }

        [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl2
        {
            // IAudioSessionControl
            void GetState(out AudioSessionState state);
            void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string? name);
            void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);
            void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string? path);
            void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);
            void GetGroupingParam(out Guid groupingParam);
            void SetGroupingParam(ref Guid overrideValue, ref Guid eventContext);
            void RegisterAudioSessionNotification(IntPtr newNotifications);
            void UnregisterAudioSessionNotification(IntPtr newNotifications);
            // IAudioSessionControl2
            void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string? id);
            void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string? id);
            void GetProcessId(out uint pid);
            [PreserveSig] int IsSystemSoundsSession();   // S_OK (0) = system sounds, S_FALSE (1) = not
        }

        [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISimpleAudioVolume
        {
            void SetMasterVolume(float level, ref Guid eventContext);
            void GetMasterVolume(out float level);
            void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
            void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }
    }
}
