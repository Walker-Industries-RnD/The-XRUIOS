using System.Numerics;
using YuukoProtocol;


namespace XRUIOS.Barebones.Interfaces
{
    public static class AreaManagerClass
    {


        public enum PositionalTrackingMode { Follow, Anchored, FollowingExternal }
        public enum RotationalTrackingMode { Static, LAM }
        public enum ObjectOSLabel { Default, Software, Objects, Voice, WorldPoint, Alerts, Ui, Other }
        public enum RenderingMode { OnlyWhenVisible, AllFrames };


        //For referncing objects, they are imported using modtool but are at a different list. 

        //Naming = Name.AR/VR

        //3D models and 2D "dumb" stuff (file previews)

        public record WorldPoint
        {
            public RenderingMode RenderingMode;
            public byte[] PointData; // Point data, use per OS
            public string Mode;
            public string View;
            public string PointName;
            public string PointDescription;

            public FileRecord PointImagePath;
            public bool UserCentric; // Is this a point which is at a fixed point relative to the user
            public List<StaticObject> StaticObjs;
            public List<App> AppObjs;
            public List<DesktopScreen> DesktopScreenObjs;
            public List<StaciaItems> StaciaObjs;
            public string Identifier;

            public WorldPoint() { }

            public WorldPoint(RenderingMode renderingMode, byte[] pointData, string pointName, string pointDescription, FileRecord pointImagePath,
                bool userCentric, List<StaticObject> staticObjs, List<App> appObjs,
                List<DesktopScreen> desktopScreenObjs, List<StaciaItems> staciaObjs, string? identifier)
            {
                RenderingMode = renderingMode;
                PointData = pointData;
                PointName = pointName;
                PointDescription = pointDescription;
                PointImagePath = pointImagePath;
                UserCentric = userCentric;
                StaticObjs = staticObjs;
                AppObjs = appObjs;
                DesktopScreenObjs = desktopScreenObjs;
                StaciaObjs = staciaObjs;

                Identifier = identifier ?? Guid.NewGuid().ToString();
            }
        }


        public record StaticObject
        {
            public PositionalTrackingMode? PTrackingType;
            public RotationalTrackingMode? RTrackingType;
            public string Name; // Path to the object
            public Vector3? SpatialData;
            public ObjectOSLabel ObjectLabel;
            public FileRecord? AssetFile; // Reference to the file

            public StaticObject(
                PositionalTrackingMode? pTrackingType,
                RotationalTrackingMode? rTrackingType,
                string name,
                Vector3? spatialData,
                ObjectOSLabel objectLabel,
                FileRecord? assetFile)
            {
                PTrackingType = pTrackingType;
                RTrackingType = rTrackingType;
                Name = name;
                SpatialData = spatialData;
                ObjectLabel = objectLabel;
                AssetFile = assetFile;
            }

            public StaticObject() { }
        }

        // For apps
        public record App
        {
            public PositionalTrackingMode? PTrackingType;
            public RotationalTrackingMode? RTrackingType;
            public Vector3? SpatialData;
            public ObjectOSLabel ObjectLabel;
            public Handle? Reference;
            // Reference can be:
            // - a desktop ID for a running app
            // - a local app name/path
            // - a YuukoApp for cross-device apps
            // - a direct file path
            // - binary data for ad-hoc apps
            // DeviceOrigin tells which device this lives on
            // CanSendCommands indicates if we can send remote commands
            // DefaultCommand can provide a startup instruction

            public App(
                PositionalTrackingMode? pTrackingType,
                RotationalTrackingMode? rTrackingType,
                Vector3? spatialData,
                ObjectOSLabel objectLabel,
                Handle? reference)
            {
                PTrackingType = pTrackingType;
                RTrackingType = rTrackingType;
                SpatialData = spatialData;
                ObjectLabel = objectLabel;
                Reference = reference;
            }

            public App() { }
        }

        // Use for desktop apps
        public record DesktopScreen
        {
            public PositionalTrackingMode? PTrackingType;
            public RotationalTrackingMode? RTrackingType;
            public Vector3? SpatialData;
            public ObjectOSLabel ObjectLabel;
            public Handle? Reference;
            // Works same as App: represents the actual desktop program,
            // remote or local, possibly on another device

            public DesktopScreen(
                PositionalTrackingMode? pTrackingType,
                RotationalTrackingMode? rTrackingType,
                Vector3? spatialData,
                ObjectOSLabel objectLabel,
                Handle? reference)
            {
                PTrackingType = pTrackingType;
                RTrackingType = rTrackingType;
                SpatialData = spatialData;
                ObjectLabel = objectLabel;
                Reference = reference;
            }

            public DesktopScreen() { }
        }

        // Use this for stuff like widgets, temporary items, etc.
        public record StaciaItems
        {
            public PositionalTrackingMode? PTrackingType;
            public RotationalTrackingMode? RTrackingType;
            public Vector3? SpatialData;
            public ObjectOSLabel ObjectLabel;
            public Handle? Reference;
            // Widgets or dynamic items can reference:
            // - a local script or binary
            // - a YuukoApp mini-component
            // - a file or byte[] depending on context
            // This allows remote execution or interaction

            public StaciaItems(
                PositionalTrackingMode? pTrackingType,
                RotationalTrackingMode? rTrackingType,
                Vector3? spatialData,
                ObjectOSLabel objectLabel,
                Handle? reference)
            {
                PTrackingType = pTrackingType;
                RTrackingType = rTrackingType;
                SpatialData = spatialData;
                ObjectLabel = objectLabel;
                Reference = reference;
            }

            public StaciaItems() { }
        }




        //Patchers

        public sealed record WorldPointPatch
        {
            public string? PointName { get; init; }
            public string? PointDescription { get; init; }
            public FileRecord? PointImagePath { get; init; }
            public bool? UserCentric { get; init; }
            public List<StaticObject>? StaticObjs { get; init; }
            public List<App>? AppObjs { get; init; }
            public List<DesktopScreen>? DesktopScreenObjs { get; init; }
            public List<StaciaItems>? StaciaObjs { get; init; }
        }

        public sealed record StaticObjectPatch
        {
            public PositionalTrackingMode? PTrackingType { get; init; }
            public RotationalTrackingMode? RTrackingType { get; init; }
            public string? Name { get; init; }
            public Vector3? SpatialData { get; init; }
            public ObjectOSLabel? ObjectLabel { get; init; }
            public FileRecord? AssetFile { get; init; }
        }

        public sealed record AppPatch
        {
            public PositionalTrackingMode? PTrackingType { get; init; }
            public RotationalTrackingMode? RTrackingType { get; init; }
            public Vector3? SpatialData { get; init; }
            public ObjectOSLabel? ObjectLabel { get; init; }
            public Handle? Reference { get; init; }
        }

        public sealed record DesktopScreenPatch
        {
            public PositionalTrackingMode? PTrackingType { get; init; }
            public RotationalTrackingMode? RTrackingType { get; init; }
            public Vector3? SpatialData { get; init; }
            public ObjectOSLabel? ObjectLabel { get; init; }
            public Handle? Reference { get; init; }
        }

        public sealed record StaciaItemsPatch
        {
            public PositionalTrackingMode? PTrackingType { get; init; }
            public RotationalTrackingMode? RTrackingType { get; init; }
            public Vector3? SpatialData { get; init; }
            public ObjectOSLabel? ObjectLabel { get; init; }
            public Handle? Reference { get; init; }
        }


    }
}
