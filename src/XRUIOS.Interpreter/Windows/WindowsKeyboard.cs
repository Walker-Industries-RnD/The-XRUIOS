using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace XRUIOS.Interpreter.Windows
{
    /// <summary>
    /// The user's installed input layouts (the language bar list). Ids are the HKL as 8 hex digits,
    /// e.g. "04090409" = English (US) / US. Applying one switches the foreground window immediately
    /// and makes it the session default for windows opened afterwards.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class WindowsKeyboard : IKeyboardInterpreter
    {
        private const string LayoutsKey = @"SYSTEM\CurrentControlSet\Control\Keyboard Layouts";

        public IReadOnlyList<KeyboardLayout> ListLayouts()
            => InstalledHkls().Select(h => new KeyboardLayout(IdOf(h), NameOf(h))).ToList();

        public string? GetActiveLayout()
        {
            var fg = Native.GetForegroundWindow();
            uint thread = fg == IntPtr.Zero ? 0 : Native.GetWindowThreadProcessId(fg, out _);
            var hkl = Native.GetKeyboardLayout(thread);
            return hkl == IntPtr.Zero ? null : IdOf(hkl);
        }

        public void ApplyLayout(string layoutId)
        {
            var hkl = InstalledHkls().FirstOrDefault(h => string.Equals(IdOf(h), layoutId, StringComparison.OrdinalIgnoreCase));
            if (hkl == IntPtr.Zero)
                throw new KeyNotFoundException($"Keyboard layout '{layoutId}' isn't installed. Add it in Windows language settings first.");

            // New windows pick up the session default; the focused window needs the change request.
            var boxed = hkl;
            if (!Native.SystemParametersInfo(Native.SPI_SETDEFAULTINPUTLANG, 0, ref boxed, Native.SPIF_SENDCHANGE))
                throw Native.LastError("SystemParametersInfo(SPI_SETDEFAULTINPUTLANG)");

            var fg = Native.GetForegroundWindow();
            if (fg != IntPtr.Zero)
                Native.PostMessage(fg, Native.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hkl);
        }

        private static IntPtr[] InstalledHkls()
        {
            int n = Native.GetKeyboardLayoutList(0, null);
            var list = new IntPtr[n];
            Native.GetKeyboardLayoutList(n, list);
            return list;
        }

        private static string IdOf(IntPtr hkl) => ((uint)(hkl.ToInt64() & 0xFFFFFFFF)).ToString("X8");

        private static string NameOf(IntPtr hkl)
        {
            uint raw = (uint)(hkl.ToInt64() & 0xFFFFFFFF);
            int langId = (int)(raw & 0xFFFF);

            string language;
            try { language = CultureInfo.GetCultureInfo(langId).DisplayName; }
            catch (CultureNotFoundException) { language = $"Language 0x{langId:X4}"; }

            string? layout = LayoutText(raw);
            return layout is null || language.Contains(layout, StringComparison.OrdinalIgnoreCase)
                ? language
                : $"{language} - {layout}";
        }

        /// <summary>Resolve the HKL's keyboard layout id (KLID) and read its "Layout Text".</summary>
        private static string? LayoutText(uint hkl)
        {
            uint lang = hkl & 0xFFFF;
            uint device = hkl >> 16;
            try
            {
                using var layouts = Registry.LocalMachine.OpenSubKey(LayoutsKey);
                if (layouts is null) return null;

                string klid;
                if ((device & 0xF000) == 0xF000)
                {
                    // Variant layout (Dvorak, US-International, ...): match its "Layout Id".
                    string wanted = (device & 0x0FFF).ToString("X4");
                    klid = layouts.GetSubKeyNames().FirstOrDefault(k =>
                    {
                        using var sub = layouts.OpenSubKey(k);
                        return string.Equals(sub?.GetValue("Layout Id") as string, wanted, StringComparison.OrdinalIgnoreCase);
                    }) ?? "";
                }
                else if ((device & 0xF000) == 0xE000)
                {
                    klid = hkl.ToString("X8");            // IME: the HKL is the KLID
                }
                else
                {
                    klid = device == 0 ? lang.ToString("X8") : device.ToString("X8");
                }

                using var key = klid.Length > 0 ? layouts.OpenSubKey(klid) : null;
                return key?.GetValue("Layout Text") as string;
            }
            catch { return null; }
        }
    }
}
