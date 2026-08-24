using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XRUIOS.Barebones.Interfaces
{
    public class DeviceManagerClass
    {

        public record Device
        {
            public string Name;
            public string Description;
            public Guid UUID;

            public OS OperatingSystem;

            // Algorithm / Key ID -> Public Key
            public Dictionary<string, byte[]> PublicKey;

            // NEVER send this outside the device.
            public Dictionary<string, byte[]>? PrivateKey;

            // Displays / rendering targets available on the device.
            public List<RenderSource> Renders;

            // Capabilities supported by this device.
            public HashSet<DeviceCapability> Capabilities;

            public Device() { }

            public Device(
                string name,
                string description,
                Guid uuid,
                OS operatingSystem,
                Dictionary<string, byte[]> publicKey,
                List<RenderSource>? renders = null,
                HashSet<DeviceCapability>? capabilities = null,
                Dictionary<string, byte[]>? privateKey = null)
            {
                Name = name;
                Description = description;
                UUID = uuid;
                OperatingSystem = operatingSystem;

                PublicKey = publicKey;
                PrivateKey = privateKey;

                Renders = renders ?? [];
                Capabilities = capabilities ?? [];
            }

            public bool Supports(DeviceCapability capability)
                => Capabilities.Contains(capability);
        }

        public enum DeviceCapability
        {
            CMD,

            UI2D,
            UI3D,

            Touch,
            HandTracking,
            EyeTracking,

            InsideOutTracking,
            SpatialAnchors,

            Passthrough,
            DepthSensing,

            AudioInput,
            AudioOutput,

            Camera,

            Microphone,

            Notifications,

            Networking,

            Bluetooth,
            WiFi,

            GPUCompute,
            HardwareAcceleration
        }

        public record RenderSource
        {
            public string Name;
            public List<ModeViews> SupportedModeViews;

            public RenderSource() { }

            public RenderSource(string name, List<ModeViews> supportedModeViews)
            {
                Name = name;
                SupportedModeViews = supportedModeViews;
            }

        }

        public record ModeViews
        {
            public Modes Mode;
            public List<SupportedViews> Views;

            public ModeViews() { }

            public ModeViews(Modes mode, List<SupportedViews> views)
            {
                Mode = mode;
                Views = views;
            }
        }

        public enum OS { Windows, Linux, Android, Other}
        public enum Modes { Object, Panel, Minimized}

        //Objects can be spawned into 3D spaces
        //Panels can be rendered onto 2D viewports
        //Minimized systems are for low resolution experiences

        public enum SupportedViews { ThreeD, TwoD, CMD}

        //3D = VR/AR/XR and even generl 3D spaces (FPS)
        //2D = Windows, generic hosts
        //CMD = Command Line Interfaces






    }
}
