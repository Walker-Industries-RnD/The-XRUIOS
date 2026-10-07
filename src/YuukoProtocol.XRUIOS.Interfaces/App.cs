using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YuukoProtocol
{
    public class App
    {
        public record Overview(string YuukoAppName, string MinimumYuukoVersion, string DeveloperID, Dictionary<string, byte[]> PublicKey, string UUID, string AppName, string Description, string PrimaryPlatform);
        public record Parameter(string Name, Type ParamType, bool Required, object? Default, List<object>? Choices);
        public record Event(string Name, string Description, List<Parameter>? Parameters, string? Command);
        public record OSSpecificApp(string OS, string Version, string Download, List<Panel> PanelInfo);
        public record Panel(string Description, List<RenderStyle> Renders);
        public record RenderStyle(string Mode, string View);
        public record PreferredPlatformMapping(string? Mode2D, string? Mode3D, string? ModeCMD);
        public record YuukoApp(Overview YuukoInfo, Dictionary<string, PreferredPlatformMapping> Defaults, List<OSSpecificApp> OSSet, Dictionary<string, List<Event>> OSSpecificEntrypoints, List<Event> SharedEntrypoints);

    }

}
