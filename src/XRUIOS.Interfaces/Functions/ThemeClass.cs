

namespace XRUIOS.Barebones.Interfaces
{
    public static class ThemeSystem
    {

        public record ThemeColors
        {
            public (string, string) BackgroundPrimary; //Image, color
            public (string, string) BackgroundSecondary;
            public (string, string) Surface;
            public (string, string) AccentPrimary;
            public (string, string) AccentSecondary;
            public string TextPrimary;
            public string TextMuted;
            public string Error;
            public string Warning;
            public string Success;

            public ThemeColors() { }

            public ThemeColors(
                (string, string) backgroundPrimary,
                (string, string) backgroundSecondary,
                (string, string) surface,
                (string, string) accentPrimary,
                (string, string) accentSecondary,
                string textPrimary,
                string textMuted,
                string error,
                string warning,
                string success)
            {
                BackgroundPrimary = backgroundPrimary;
                BackgroundSecondary = backgroundSecondary;
                Surface = surface;
                AccentPrimary = accentPrimary;
                AccentSecondary = accentSecondary;
                TextPrimary = textPrimary;
                TextMuted = textMuted;
                Error = error;
                Warning = warning;
                Success = success;
            }
        }

        public record ThemeTypography
        {
            public List<string> PrimaryFont; //h1 = h6, paragraph, caption
            public float FontScale;

            public ThemeTypography() { }

            public ThemeTypography(
                List<string> primaryFont,
                float fontScale)
            {
                PrimaryFont = primaryFont;
                FontScale = fontScale;
            }
        }

        public record ThemeSpatial
        {
            public float PanelThickness;
            public float CornerRadius;
            public float PanelCurvature;
            public string PhysicalityPreset;
            public bool EnableVolumetricShadows;

            public ThemeSpatial() { }

            public ThemeSpatial(
                float panelThickness,
                float cornerRadius,
                float panelCurvature,
                string physicalityPreset,
                bool enableVolumetricShadows)
            {
                PanelThickness = panelThickness;
                CornerRadius = cornerRadius;
                PanelCurvature = panelCurvature;
                PhysicalityPreset = physicalityPreset;
                EnableVolumetricShadows = enableVolumetricShadows;
            }
        }

        public sealed class UIAudioRoles
        {
            public string? Navigate;   // focus change, cursor move, gaze hop
            public string? Select;     // confirm / primary action
            public string? Back;       // cancel / escape
            public string? Error;      // invalid action
            public string? Warning;    // “hey idiot, careful”
            public string? Success;    // completion, save, done
            public string? Disabled;   // interaction blocked
            public string? Hover;      // subtle, optional

            // Parameterless constructor
            public UIAudioRoles() { }

            // Full constructor
            public UIAudioRoles(
                string? navigate,
                string? select,
                string? back,
                string? error,
                string? warning,
                string? success,
                string? disabled,
                string? hover)
            {
                Navigate = navigate;
                Select = select;
                Back = back;
                Error = error;
                Warning = warning;
                Success = success;
                Disabled = disabled;
                Hover = hover;
            }
        }

        public sealed class AppAudioRoles
        {
            public string? Launch;
            public string? Close;
            public string? Crash;       // yes, really    ok gang I got it
            public string? Background;
            public string? Foreground;

            // Parameterless constructor
            public AppAudioRoles() { }

            // Full constructor
            public AppAudioRoles(
                string? launch,
                string? close,
                string? crash,
                string? background,
                string? foreground)
            {
                Launch = launch;
                Close = close;
                Crash = crash;
                Background = background;
                Foreground = foreground;
            }
        }

        public record ThemeIdentity
        {
            public string ThemeID;
            public string Name;
            public string Author;
            public string Version;
            public List<string> TargetModes;


            public ThemeIdentity() { }

            public ThemeIdentity(
                string themeID,
                string name,
                string author,
                string version,
                List<string> targetModes)
            {
                ThemeID = themeID;
                Name = name;
                Author = author;
                Version = version;
                TargetModes = targetModes;

            }
        }

        public record DefaultApp
        {
            public string AppID;            // Unique identifier
            public string Role;             // Launcher, Calendar, Browser, etc
            public int LaunchPriority;      // Lower = earlier
            public bool AutoStart;          // Should it start automatically
            public string? CustomPanel;     // Optional: panel ID if tied to a panel
            public List<DefaultAppImage> Images;
            public List<ThemeTypography> FontOverrides;
            public List<DefaultAppSound> SoundOverrides;


            public DefaultApp() { }

            public DefaultApp(string appID, string role, int launchPriority, bool autoStart,
                List<DefaultAppImage> images, List<ThemeTypography> fontOverrides,
                List<DefaultAppSound> soundOverrides, string? customPanel = null)
            {
                AppID = appID;
                Role = role;
                LaunchPriority = launchPriority;
                AutoStart = autoStart;
                CustomPanel = customPanel;
                Images = images;
                FontOverrides = fontOverrides;
                SoundOverrides = soundOverrides;
            }

        }

        public record DefaultAppSound
        {
            public string Role;          // Launcher, Calendar, Notification, etc
            public string Path;          // Path to sound file
            public float Volume;         // 0-1
            public float Pitch;          // Optional pitch modification
            public bool IsDefault;

            public DefaultAppSound() { }

            public DefaultAppSound(string role, string path, float volume = 1f, float pitch = 1f, bool isDefault = false)
            {
                Role = role;
                Path = path;
                Volume = volume;
                Pitch = pitch;
                IsDefault = isDefault;
            }
        }

        public record DefaultAppImage
        {
            public string Role;          // Launcher, Calendar, Browser, etc
            public string Path;          // Path to image
            public int Width;            // Pixels
            public int Height;           // Pixels
            public float AspectRatio;    // Width / Height
            public bool IsDefault;       // True = primary image for this role

            public DefaultAppImage() { }

            public DefaultAppImage(string role, string path, int width, int height, bool isDefault = false)
            {
                Role = role;
                Path = path;
                Width = width;
                Height = height;
                AspectRatio = width / (float)height;
                IsDefault = isDefault;
            }
        }


        public record XRUIOSTheme
        {
            public ThemeIdentity Identity;
            public ThemeColors Colors;
            public ThemeTypography Typography;
            public ThemeSpatial Spatial;
            public AppAudioRoles AppAudio;
            public UIAudioRoles Audio;
            public List<DefaultApp> Defaults;

            public XRUIOSTheme() { }

            public XRUIOSTheme(
                ThemeIdentity identity,
                ThemeColors colors,
                ThemeTypography typography,
                ThemeSpatial spatial,
                AppAudioRoles appAudio,
                UIAudioRoles audio,
                List<DefaultApp> defaults)
            {
                Identity = identity;
                Colors = colors;
                Typography = typography;
                Spatial = spatial;
                AppAudio = appAudio;
                Audio = audio;
                Defaults = defaults;
            }
        }


    }

}
