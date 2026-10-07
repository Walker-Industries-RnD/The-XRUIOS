# XRUIOS UI Apps (OpenSilver + Photino)

Every folder in `XRUIOS Desktop/` has a scaffold here. Each app lives in its
own folder under `src/apps/`, generated from the official OpenSilver template
(`dotnet new opensilverapp` with the `usePhotino` flag), with a per-app
custom-controls library added alongside.

## Layout

```
src/apps/
├── Directory.Build.props           memory / trim settings for every app
├── XRUIOS.Apps.Shared/             cross-app: broker, widget chrome, holes
└── XRUIOS.App.<Name>/              one folder per app, own .sln
    ├── XRUIOS.App.<Name>/               OpenSilver UI (App.xaml + MainPage.xaml)
    ├── XRUIOS.App.<Name>.Browser/       Blazor WASM browser host
    ├── XRUIOS.App.<Name>.Photino/       Photino desktop host  (production shell)
    ├── XRUIOS.App.<Name>.Simulator/     WPF simulator          (fast dev loop)
    ├── XRUIOS.App.<Name>.Controls/      per-app custom controls (in code)
    ├── XRUIOS.App.<Name>.sln             solution for THIS app only
    └── NuGet.Config                      OpenSilver MyGet feed
```

Every app is also listed in the top-level `XRUIOS.slnx` so `dotnet build` at
the repo root builds everything at once. Per-app `.sln`s exist so you can open
one app at a time in Visual Studio without loading the whole XRUIOS graph.

## How each app was generated

```bash
cd src/apps/XRUIOS.App.<Name>
dotnet new opensilverapp -n XRUIOS.App.<Name> --usePhotino
dotnet new opensilverlib -n XRUIOS.App.<Name>.Controls -o XRUIOS.App.<Name>.Controls
dotnet sln add XRUIOS.App.<Name>.Controls
dotnet sln add ../XRUIOS.Apps.Shared
dotnet add XRUIOS.App.<Name>/*.csproj reference XRUIOS.App.<Name>.Controls
dotnet add XRUIOS.App.<Name>/*.csproj reference ../XRUIOS.Apps.Shared
```

Nothing bespoke - just the official templates plus the two references above.

## Fullscreen vs widget

Design rule: **anything whose mockup isn't 1920x1080 is a widget** - chromeless,
transparent, always-on-top by default, dragged by its top strip.

| Kind        | Apps                                                         |
|-------------|--------------------------------------------------------------|
| Fullscreen  | Calendar · Gallery · ThemeEditor · Time · Weather · WorldEvents |
| Widget      | AppLauncher · Clipboard · Keyboard · Notifications · PocketPal · Power · VolumeManager |

The choice lives only in the app's `Photino/Program.cs` - the OpenSilver
project is identical either way (both kinds import `WidgetChrome` from Shared;
widget MainPage wraps its content in `<w:WidgetChrome>`, fullscreen MainPage
just paints the whole page).

## Memory settings

`src/apps/Directory.Build.props` applies OpenSilver's own recommendations
([docs](https://doc.opensilver.net/documentation/general/optimizing-app-size-and-performance.html))
to every project under this folder:

- `InvariantGlobalization=true` - drops ICU (~8 MB).
- `BlazorEnableCompression=true` - brotli the wasm.
- `WasmStripILAfterAOT=true` - strip IL after AOT.
- `PublishTrimmed=true` + `TrimMode=partial` - dead-code walk without breaking
  XAML reflection. Flip a single app to `full` once you've verified nothing
  reflects on unreferenced types.
- `TieredCompilation=true` - hot methods keep JIT tier, cold drops.
- `ServerGarbageCollection=false` - workstation GC keeps the widget footprint
  smaller than server GC would.

Per-app control libraries exist so the trimmer can drop what one app doesn't
touch. Cross-app pieces stay in `XRUIOS.Apps.Shared`.

## Holes (integration points)

A **hole** is a small `static class` under `XRUIOS.Apps.Shared/Holes/`, each
one wrapping a worker capability with a clean API a XAML page can call in one
line. Adding a hole = one file. Using a hole = one line.

```csharp
var cals = await CalendarPicker.ListAsync();
```

| Hole                   | Backing worker(s)                          | Used by            |
|------------------------|---------------------------------------------|--------------------|
| `CalendarPicker`       | `XRUIOS.Worker.Calendar`                    | Calendar           |
| `MediaAlbumPicker`     | `XRUIOS.Worker.MediaAlbum`                  | Gallery            |
| `AppLaunchTarget`      | `XRUIOS.Worker.App`                         | AppLauncher, PocketPal |
| `ThemePicker`          | `XRUIOS.Worker.Theme`                       | ThemeEditor, all   |
| `ClipboardHistory`     | `XRUIOS.Worker.Clipboard`                   | Clipboard          |
| `NotificationFeed`     | `XRUIOS.Worker.Notification`                | Notifications      |
| `VolumeMixer`          | `XRUIOS.Worker.Volume`                      | VolumeManager      |
| `PowerControl`         | `XRUIOS.Worker.SystemInfo`                  | Power              |
| `WeatherFeed`          | `XRUIOS.Worker.Geo` + weather cap           | Weather            |
| `WorldEventsFeed`      | `XRUIOS.Worker.WorldEvents`                 | WorldEvents        |
| `TimeSource`           | `XRUIOS.Worker.Chrono`/`.Alarm`/`.Timer`    | Time               |
| `PocketPalIntent`      | YuukoProtocol                               | PocketPal          |
| `KeyboardLayoutPicker` | `XRUIOS.Worker.DeviceManager`               | Keyboard           |

Each `<Name>Root` control inside `XRUIOS.App.<Name>.Controls/AppRoot.cs` calls
its own hole in `HydrateAsync()`; that's the seam where a designer or a
follow-up pass replaces the placeholder rows with the real mockup layout.

## Widget chrome (the drag)

`XRUIOS.Apps.Shared/Widgets/WidgetChrome.xaml` is a `UserControl` with a top
strip, a pin button, and a close button. On press on the strip it raises
`DragStarted`; the Photino host handles that IPC by asking Photino for a
native window move so the whole widget follows the cursor. Pin flips
`TopMost`; close hides the window (the tray reopens widgets - they never fully
exit).

## Boot order

1. Photino host process starts.
2. `PhotinoWindow.ConfigureOpenSilver<App>()` hosts the OpenSilver runtime;
   `Load("wwwroot/index.html")` boots it.
3. `App.xaml.cs` sets `RootVisual = new MainPage()` (from the template).
4. `MainPage.Loaded` calls `AppShell.InitializeAsync("XRUIOS.App.<Name>")`,
   which reads `XRUIOS_APP_ID` / `XRUIOS_APP_PSK` / `XRUIOS_BROKER_ADDR` from
   the environment (the Manager sets these when it launches an app) and opens
   an `EclipseSecureClient` to the broker.
5. `MainPage` then calls `AppRoot.HydrateAsync()`; that hits the app's hole.
