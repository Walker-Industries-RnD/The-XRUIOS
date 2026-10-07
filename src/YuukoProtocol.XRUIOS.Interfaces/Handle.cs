

using static YuukoProtocol.App;

namespace YuukoProtocol
{
    public record Handle(
        int? Desktop = null,
        string? LocalApp = null,
        YuukoApp? YuukoApp = null,
        string? AppPath = null,
        byte[]? BinaryData = null,
        DirectoryRecord? DirectoryRef = null,
        string DeviceOrigin = "",
        string? DefaultCommand = null
    );
}
