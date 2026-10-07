using YuukoProtocol;


namespace XRUIOS.Barebones.Interfaces
{
    public static class AppClass
    {


        //Each app has an optional YuukoApp; it allows us to know what apps exist as an equivalent on other devices! Can be dev or user set

        public record XRUIOSAppManifest
        {
            public string AppId;
            public string Name;
            public string Description;
            public string Author;
            public string Version;

            public FileRecord? YuukoAppInfo;

            public string EntryPoint;

            public string Identifier;


            public XRUIOSAppManifest() { }

            public XRUIOSAppManifest(
                string appID,
                string name,
                string description,
                string author,
                string version,
                FileRecord? yuukoAppInfo,
                string entryPoint, string? identifier)
            {
                AppId = appID;
                Name = name;
                Description = description;
                Author = author;
                Version = version;
                YuukoAppInfo = yuukoAppInfo;
                EntryPoint = entryPoint;
                Identifier = identifier ?? Guid.NewGuid().ToString();
            }
        }

        public record XRUIOSAppManifestPatch
        {
            public string? AppId { get; init; }
            public string? Name { get; init; }
            public string? Description { get; init; }
            public string? Author { get; init; }
            public string? Version { get; init; }
            public FileRecord? YuukoAppInfo;
            public string? EntryPoint;

            public string? Identifier;
        }

    }

}