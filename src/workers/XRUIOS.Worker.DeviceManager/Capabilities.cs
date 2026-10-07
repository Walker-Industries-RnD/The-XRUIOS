using EclipseProject;
using XRUIOS.Interfaces;
using XRUIOS.Interpreter;

namespace XRUIOS.Worker.DeviceManager
{
    // Input devices on THIS machine. ListKeyboardLayouts/ApplyKeyboardLayout are what the
    // KeyboardLayoutPicker hole calls - switching the OS layout keeps the on-screen keyboard and the
    // characters it produces in agreement.
    public static class DeviceManagerCapabilities
    {
        private static IKeyboardInterpreter Keyboard => XruiosInterpreter.Current.Keyboard;

        [SeaOfDirac("DeviceManager.Ping", new[] { "input" }, typeof(string), typeof(string))]
        public static string Ping(string input) => "XRUIOS.Worker.DeviceManager: " + input;

        /// <summary>"id|name;..." for the layouts installed on this machine.</summary>
        [SeaOfDirac("ListKeyboardLayouts", new string[0], typeof(string))]
        public static string ListKeyboardLayouts() => Wire.Layouts(Keyboard.ListLayouts());

        [SeaOfDirac("ActiveKeyboardLayout", new string[0], typeof(string))]
        public static string ActiveKeyboardLayout() => Keyboard.GetActiveLayout() ?? "";

        [SeaOfDirac("ApplyKeyboardLayout", new[] { "layoutId" }, typeof(string), typeof(string))]
        public static string ApplyKeyboardLayout(string layoutId)
        {
            Keyboard.ApplyLayout(layoutId);
            WorkerEvents.Publish(XruiosEventNames.KeyboardLayoutChanged, EventJson.Write(new { layoutId }));
            return "ok";
        }
    }
}
