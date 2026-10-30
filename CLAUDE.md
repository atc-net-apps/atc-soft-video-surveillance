# AtcSoft.VideoSurveillance - Claude Code Guidelines

## Project Overview
A multi-assembly video surveillance platform with a WPF desktop app and a headless REST API server (live events over Server-Sent Events), sharing a common Core library. Uses RTSP/HTTP protocols with AtcSoft.VideoEngine (in-process FFmpeg) for video on both WPF and server.

## Solution Structure
- `AtcSoft.VideoSurveillance.Core` - Shared library: models, enums, events, service interfaces, helpers (net10.0, no WPF)
- `AtcSoft.VideoSurveillance.Resources` - The texts of every client: `Translations.resx` in US English (neutral), `en-GB`, `da-DK`, `de-DE` (net10.0, no WPF, published)
- `AtcSoft.VideoEngine` - Cross-platform video engine (net10.0): demux, decode, record, capture via in-process FFmpeg
- `AtcSoft.VideoEngine.DirectX` - D3D11VA GPU acceleration, Video Processor, swap chain (net10.0-windows)
- `AtcSoft.VideoEngine.Windows` - Windows-only parts shared by the standalone app and the server, not published: USB camera enumeration (Media Foundation) + hot-plug watcher (WMI), and the recording thumbnail generator (GDI+) (net10.0-windows)
- `AtcSoft.VideoPlayer.Wpf` - WPF VideoHost control with DComp surface + XAML overlay (net10.0-windows)
- `AtcSoft.CameraWall.Wpf` - WPF library with the complete camera wall: UI, dialogs, VideoEngine integration (not published; consumed by the CameraWall app)
- `AtcSoft.CameraWall.Wpf.App` - Thin shell WPF application using the library (standalone, connects directly to cameras)
- `AtcSoft.VideoSurveillance.Wpf` - WPF library for the VideoSurveillance API client (GatewayService, SurveillanceEventService, OpenAPI-generated client)
- `AtcSoft.VideoSurveillance.Wpf.App` - WPF management application for the API server (Fluent.Ribbon, Serilog, splash screen)
- `AtcSoft.VideoSurveillance.Api.Contracts` - Generated API models, endpoints, handler interfaces (OpenAPI-first via Atc.Rest.Api.SourceGenerator 3.0.1 from nuget.org)
- `AtcSoft.VideoSurveillance.Api.Domain` - Handler implementations calling Core services
- `AtcSoft.VideoSurveillance.Api` - ASP.NET Core host (REST, `GET /events`, HLS streams)
- `AtcSoft.VideoSurveillance.Aspire` - Aspire AppHost for orchestrated startup and developer dashboard
- `AtcSoft.VideoSurveillance.Blazor.App` - Blazor WebAssembly client for the API server (MudBlazor). Its logic lives in plain classes under `Logic/`, tested in `AtcSoft.VideoSurveillance.Blazor.App.Tests`
- `AtcSoft.VideoSurveillance.Storybook` - Scenes with known content for measuring motion detection: the storybook model, a deterministic renderer with its truth, the scorer (net10.0, not published; see `docs/motion-detection-storybook.md`)
- `sample/BoundingBoxWpfApp` - Developer tool, not published: a storybook or a recording through the real detector and box overlay, picture by picture, with the truth and the score; walked by `BoundingBoxSampleSmokeTests`
- `test/*` - xUnit v3 test projects, one per source project that has tests (Core, VideoEngine, VideoEngine.Windows, Api, Wpf.Core, Wpf, CameraWall.Wpf, Blazor.App, Storybook), plus `AtcSoft.VideoSurveillance.E2E.Tests` (smoke tests of the running apps, see below). The storybook tests keep every detector score in `baseline.json` and fail when one gets worse; `ATCSOFT_BBOX_UPDATE_BASELINE=1` writes new scores after an improvement. A scene with `video.bitrateKbps` is encoded by the FFmpeg tool and scored as the apps read a recording; it skips without FFmpeg

## NuGet Packages
Six projects are published to nuget.org so external WPF apps can embed live camera tiles:

| Package | Role |
|---|---|
| `AtcSoft.VideoSurveillance.Wpf.Core` | **Entry point for consumers** — `CameraTile`, `CameraGrid`, `CameraOverlay` |
| `AtcSoft.VideoSurveillance.Core` | `CameraConfiguration` and the other shared models |
| `AtcSoft.VideoPlayer.Wpf` | `VideoHost` control |
| `AtcSoft.VideoEngine.DirectX` | D3D11VA rendering |
| `AtcSoft.VideoEngine` | FFmpeg engine, including `VideoPlayerFactory` |
| `AtcSoft.VideoSurveillance.Resources` | The texts in four languages, used by `Wpf.Core` |

These are exactly the projects with `<IsPackable>true</IsPackable>`; every other project is `false`. They **must ship together at the same version**: packing turns each `ProjectReference` into a NuGet dependency pinned to that version, so publishing a package without its dependencies leaves it uninstallable. The release workflow packs the whole solution for that reason, then asserts every package actually appears on nuget.org, since `--skip-duplicate` can mask rejected pushes.

Marking another project packable means it joins the lockstep, and its package id must be covered by the nuget.org Trusted Publishing policy's glob patterns.

A consumer must call `CameraTile.InitializeServices(..., videoPlayerFactory: ...)` before the tile plays anything — without a factory it stays `Disconnected`. The packages do not include the FFmpeg 9 native DLLs (`avutil-61.dll` etc.); the consuming app deploys them itself or calls `FFmpegSetup.EnsureReady()`.

**Versions and commits** (as atc-wpf): release-please makes the version and `CHANGELOG.md` from Conventional Commits (`feat(scope): …`, `fix(scope): …`, `docs`, `perf`, `refactor`; config in `release-please-config.json`). A change to the packages' public API, breaking or not, goes as a plain `feat` or `fix` that names the change in its body; never `!` or a `BREAKING CHANGE:` footer, which would move the packages to a new major version. A new major version is decided on its own, with a `Release-As:` footer.

## Architecture
The solution follows a layered architecture with Core at the base:
- **Core** contains all shared models, enums, events, service interfaces, and helpers (zero UI dependencies)
- **VideoEngine** provides cross-platform video: demuxing, decoding (CPU/D3D11VA), recording, snapshots. DirectX project adds GPU rendering for WPF.
- **VideoEngine.Windows** provides USB / DirectShow source support: Media Foundation device enumeration, WMI hot-plug events, and the dshow demuxer wiring needed by the engine to open `video=Friendly Name` sources.
- **CameraWall WPF library** references Core and VideoEngine, adds UI controls, dialogs, VideoHost-based media pipeline
- **CameraWall WPF App** is a standalone thin shell with Ribbon UI delegating to `ICameraWallManager` (connects directly to cameras)
- **VideoSurveillance WPF library** references Core and VideoPlayer, provides `GatewayService` (OpenAPI REST client) and `SurveillanceEventService` (the `GET /events` reader)
- **VideoSurveillance WPF App** is an API client thin shell with Ribbon UI, connects to the REST API and its event stream (not directly to cameras)
- **API** references Core and provides REST endpoints and real-time events (`GET /events`)
- **Aspire** orchestrates the API, Blazor.App, and Wpf.App with a developer dashboard

## Server Edition
The REST API server enables headless video surveillance:
- **OpenAPI-first**: `src/VideoSurveillance.yaml` defines all endpoints; `Atc.Rest.Api.SourceGenerator` generates contracts
- **Endpoints**: Cameras CRUD, Layouts CRUD, Recordings list, Settings get/update, Snapshot capture, Recording start/stop
- **Live streams**: a viewer takes a lease (`POST /cameras/{id}/stream` -> `leaseId`, `playlistUrl`, `renewSeconds`), renews it (`PUT .../stream/{leaseId}`; `404` means start again) and gives it back (`DELETE`). The stream stops with its last lease; a lease not renewed for two intervals runs out (`StreamingService`, swept by `HlsStreamReaperService`). There is no SignalR hub any more: a new kind of live message is an event on `GET /events` or a REST call
- **Event stream**: `GET /events` streams the camera events (connection, recording, motion, USB) as Server-Sent Events, fed by the broadcaster (`HubEventMapping` -> `ToStreamEvent`) through `SurveillanceEventFeed` (replay buffer, bounded queue per reader). A new camera event goes into the stream. The WPF client reads it with `SurveillanceEventService` (its `IsConnected` drives the server status, `OnMissedEvents` a reload), the web client with `EventStreamService`, both on the generated `ReadResumingAsync`. The states use the REST values (`connected`); the WPF client reads them through `HubStates`. The ids start from the server's start time, so a client resuming after a restart is told it missed events
- **Run with Aspire**: `dotnet run --project src/AtcSoft.VideoSurveillance.Aspire` starts the API with the Aspire dashboard

### Server rules
- **Data folder:** the server takes the location of `settings.json` / `cameras.json` from `ServerDataPaths` (`Storage:DataPath`). Never read `ApplicationPaths.Default*` directly in the server.
- **Settings contract:** every setting a client can change has to survive Core -> API model -> Core. `SettingsRoundTripTests` checks all of them by reflection; a setting left out of the API on purpose is listed there with the reason.
- **Client enums are extensible:** the generated enums in both clients are structs that keep a value the client does not know yet (`IsKnown` is false). Switch on `.Value` with the `Names` constants; never `Enum.TryParse`/`Enum.Parse`/`Enum.GetValues` on them (they compile and throw); pick a value by name with `KnownValue.FromName` (WPF) or the generated constructor. `ToString()` is the wire value (`topLeft`), not the member name: use `DropDownKey.For` to find a drop-down entry.
- **Updates keep what they leave out:** an update request (`UpdateSettingsRequest`, `UpdateCameraRequest`, `UpdateLayoutRequest`) has nothing required, and the server applies only the properties that are there (`if (request.X is { } x)`), never `?? false` or `?? 0`. A client leaves out (`null`) what it does not edit; `""` or `0` is a value, not "nothing". A response lists in `required` what the server always sends.
- **One vocabulary:** values shared between clients are written the same way everywhere — video quality through `VideoQualityHelper`, colours by name.
- **Errors:** every error response is a ProblemDetails (`application/problem+json`), the `ProblemDetails` schema in the spec. Handlers return the generated factories with the reason: `NotFound("…")`, `Conflict("…")`, `ServiceUnavailable("…")`, `BadRequest("…")`. Handler tests assert the generated result's `StatusCode` (and read `Problem`, `Value`, `Location`), not the result type. Limits on a value (`minLength`, `minimum`, ...) go in the spec, where Atc.Rest.MinimalApi's ValidationFilter checks them before the handler runs; a handler does not repeat them. The clients show the server's words through `ProblemText` (WPF: the gateway throws `HttpRequestException` with it; web: the gateway returns a `GatewayResult`).
- **Limits, caching, retries:** declared in the spec, built by the generator: `x-ratelimit-*` (snapshots, webhook tests, sign-in and setup, session renewal with a wider limit of its own), `x-cache-*` (`GET /devices/usb`, dropped on hot-plug by `UsbDeviceCacheEvictor`), `x-retry-*` (both clients call the generated `AddApiResilience()`). Only reads are retried: a new PUT or DELETE gets `x-retry-enabled: false`. A result with a header (`Location`, `Retry-After`) has it on the generated result (`Location`, `Headers`). The spec is validated `Strict`; a rule that does not fit one operation or schema is switched off there with `x-atc-suppress` and a comment giving the reason, never project-wide.
- **Enums on the wire:** an enum is written and read only as the spec's value (`rtsp`, `topLeft`). Clients and servers built on generator 2.x used the C# member name (`Rtsp`); v3 reads a value in any casing, and every value of ours differs from its member name only in casing, so 2.x and v3 clients and servers work together. Keep it so: a value with a separator (`in-progress`) would need the server and the desktop client released together.
- **Secrets in logs:** camera credentials and webhook secrets never go into a log or to a client. FFmpeg command lines and output are cleaned with `HlsStreamCommand.Redact`; a webhook receiver is named by `WebhookEndpoint.LogName`.
- **Sign-in and roles:** every operation needs a signed-in user (JWT bearer) or an API key (`X-API-Key`); the spec's root `security` says so, and a role list goes in `x-authorize-roles` (root: all three; operator and admin for changes; admin for settings, webhooks, users, API keys, audit). A new operation gets its roles in the spec, never in code. Addresses a player or an `<img>` fetches itself (`/recordings-files/...`) take the session's media token as `?media=` (`MediaAccessMiddleware`), and a live stream's files are reached only through its lease (`/streams/{leaseId:N}/`). A change that succeeds is written to the audit log by endpoint name (`AuditMiddleware`); one that should not be (sign-in, lease renewals) gets `x-audit: false` in the spec. Both clients send the credentials through the generated `AddApiCredentials` (`SessionCredentials`), which sends a token only to the operations whose `security` asks for it.
- **Secrets at rest:** camera passwords and webhook secrets are encrypted in the data folder through `SecretProtector` (Data Protection, keys in `{data}/keys`), only at the file boundary: the objects in memory keep the plain value. A new stored secret goes through it too. CameraWall does the same for its `cameras.json` with `StoredSecret` (Wpf.Core; Windows data protection for the machine, prefix `dpapi:v1:`), applied in `CameraStorageService` at save and load; a plain password in an older file is read and the file written again encrypted.
- **Host tests:** `test/AtcSoft.VideoSurveillance.Api.Tests/Host` runs the real server in-process (`ApiHostFixture`) against a temp data folder. Tests that need a camera start a live test stream with the FFmpeg command-line tool and skip without it. `CreateClient()` is signed in as the admin, `CreateAnonymousClient()` is not, and `SignInAsync` signs in another user. A rate-limited call goes through `ApiHostFixture.SendAsync` from an address of its own, so it does not use up the other tests' limit.
- See `docs/server.md`.

### Web client rules
- The web client does not reference Core. Where it needs the same rule (recording path example, default settings, colour names, video qualities) it has its own copy under `Logic/`, and a test in `AtcSoft.VideoSurveillance.Blazor.App.Tests` (which does reference Core) keeps the copy equal to the original.
- Keep logic out of the `.razor` files: counts, filters, formatting and styles are plain classes with tests.

### Texts and languages
- No literal visible text in a client: XAML uses `{atcTranslation:Resx ResxName=AtcSoft.VideoSurveillance.Resources.Translations, Key=…}`, C# and Razor `Translations.X` (with `string.Format(CultureInfo.CurrentCulture, …)` for parameters). `LiteralTextTests` finds a literal left in the API clients' views; an exception (a product name, a protocol, an example value) goes into `literal-text-allowed.txt` with the reason. Literals inside Razor `@( )` expressions and `@code` are not seen by it: review them.
- A key's last digit is its number of parameters (`LayoutRenamed2` takes `{0}` and `{1}`); a key without parameters ends in no digit (`RotationRotate90Degrees`, not `RotationRotate90`). Two keys never differ only in case: the build keeps one and drops the other. Reuse an existing text before adding a key.
- A new key gets its da-DK, de-DE and en-GB texts in the same change. Every culture file has every key (Visual Studio's resx editor reports a missing one); en-GB repeats the US text unless British English differs (colour, maximised, start-up). The resource tests fail otherwise.
- Logs, developer exceptions, the server's ProblemDetails and the wire values stay English.
- The language belongs to the client: CameraWall in `GeneralSettings.Language`, the WPF client in `ClientPreferences` (applied at startup), the web client in the browser (`LanguageChoice`, a change reloads the page). The server's `language` setting is deprecated.
- Atc.Wpf finds the texts only in loaded assemblies: each WPF app touches `Translations.ResourceManager` first thing in `OnStartup`.

## Diagnostics and end-to-end smoke tests
- **Binding errors are logged:** both WPF apps call `BindingErrorLogging.Start` (Wpf.Core) first thing, so every binding error is written to the log at Warning (`AtcSoft.BindingErrors`), with or without a debugger. A binding error is a bug: the E2E smoke tests fail on one.
- **Logs reach Aspire:** under Aspire (`OTEL_EXPORTER_OTLP_ENDPOINT` set) the server and both WPF apps also write their Serilog logs over OTLP, so the WPF apps, which have no console, show up in the dashboard's structured logs.
- **One data folder:** `ATCSOFT_DATA_PATH` moves all of a desktop app's data (ProgramData and the per-user LocalAppData files, under `{folder}\local`) into one folder. A new file a desktop app keeps goes through `ApplicationPaths` (Core: `GetLocalDataPath`, Wpf.Core: the `Default*` paths), never `Environment.GetFolderPath` directly. The AppHost passes a scratch folder to all apps with `AtcSoft__DataPath=...`.
- **E2E smoke tests:** `test/AtcSoft.VideoSurveillance.E2E.Tests` starts the server, CameraWall, the WPF client and the web client in scratch folders and opens every window, page and dialog (FlaUI for WPF, Playwright with the installed Edge for the web). They need a desktop and run only with `ATCSOFT_E2E=1`. A new dialog, view or page gets a step in its walk; controls are found by their en-US text. See `docs/e2e-smoke-tests.md`.

## Hosted Background Services (Atc.Hosting)
Long-running periodic work uses `Atc.Hosting` base classes instead of hand-rolled `System.Threading.Timer` loops. The base class supplies the loop, sequential (no-overlap) ticking, exception logging via `OnExceptionAsync`, and optional health tracking.

- **`BackgroundServiceBase<T>`** — fixed-interval work. Override `DoWorkAsync(CancellationToken)`. Cadence comes from `IBackgroundServiceOptions` (`StartupDelaySeconds`, then `RepeatIntervalSeconds` *after* each tick completes — it is delay-after-completion, not fixed-rate).
- **`BackgroundScheduleServiceBase<T>`** — cron-scheduled work via `IBackgroundScheduleServiceOptions.CronExpression` (5-field, evaluated against **UTC**). Use for wall-clock schedules ("daily at 03:00"); use `BackgroundServiceBase` for plain intervals.
- **Do not use a plain `IHostedService`.** Every hosted worker derives from an `Atc.Hosting` `*ServiceBase`, even pure event-wiring services with no recurring work. Subscribe in an overridden `StartAsync` (then call `base.StartAsync`), unsubscribe in an overridden `StopAsync` (then call `base.StopAsync`), and make `DoWorkAsync` a no-op with `RepeatIntervalSeconds = ushort.MaxValue` so the idle loop parks on a long delay rather than spinning (see `SurveillanceEventBroadcaster`).

### Per-service typed options (avoid the shared-singleton footgun)
`BackgroundServiceBase` takes the *interface* `IBackgroundServiceOptions`, so registering one `DefaultBackgroundServiceOptions` as that interface makes **every** worker share one interval. Give each worker its **own** typed options class and register it by that concrete type:
```csharp
public sealed class CameraConnectionServiceOptions : IBackgroundServiceOptions
{
    public ushort StartupDelaySeconds { get; set; } = 3;
    public ushort RepeatIntervalSeconds { get; set; } = 30;
    public string? ServiceName { get; set; } = nameof(CameraConnectionService);
}

// Program.cs
builder.Services.AddSingleton(new CameraConnectionServiceOptions());
builder.Services.AddHostedService<CameraConnectionService>();
```

### Health tracking
Register the tracker once and let each worker report staleness; the API's `/health` endpoint returns 503 when an always-on worker goes stale:
```csharp
builder.Services.AddSingleton<ITimeProvider, SystemTimeProvider>();
builder.Services.AddSingleton<IBackgroundServiceHealthService, BackgroundServiceHealthService>();
```
Each worker passes `healthService` to the 3-arg base ctor and sets its own window in its constructor:
```csharp
: base(logger, options, healthService)
{
    healthService.SetMaxStalenessInSeconds(ServiceName, BackgroundServiceHealthHelper.StalenessFor(options));
}
```

### WPF hosting (shared singleton + interface)
WPF services that are both UI-facing (events, `IsRunning`, manual triggers) and periodic derive from `BackgroundServiceBase<T>` **and** implement their interface. They are NOT `[Registration]`-auto-registered; the App registers one singleton exposed both ways so the same instance serves the UI and the host:
```csharp
services.AddSingleton<MediaCleanupService>();
services.AddSingleton<IMediaCleanupService>(sp => sp.GetRequiredService<MediaCleanupService>());
services.AddHostedService(sp => sp.GetRequiredService<MediaCleanupService>());
```
These start on `host.StartAsync()` and stop on `host.StopAsync()` — no explicit `Initialize()`/`StopService()`.

## Key Frameworks & Packages
- **Atc.XamlToolkit** - MVVM source generators (`[ObservableProperty]`, `[RelayCommand]`, `[DependencyProperty]`)
- **Atc.SourceGenerators** - Source generators for DI registration (`[Registration]`), options binding (`[OptionsBinding]`)
- **Atc.Wpf.Controls** - UI controls (`LabelTextBox`, `LabelComboBox`, `LabelPasswordBox`, `GridEx`)
- **Atc.Wpf.NetworkControls** - `NetworkScannerView` for discovering cameras
- **AtcSoft.VideoEngine** - In-process FFmpeg video engine for RTSP/HTTP streams (replaces FlyleafLib)
- **Flyleaf.FFmpeg.Bindings** - FFmpeg P/Invoke bindings NuGet dependency
- **Fluent.Ribbon** - Ribbon UI (App only)
- **Serilog** - Structured logging with file sink (App only)

## Library Services

### ICameraWallManager
Main facade for all camera wall operations. Apps inject this and delegate business logic to it.
```csharp
public interface ICameraWallManager : INotifyPropertyChanged
{
    // Properties
    ObservableCollection<CameraLayout> Layouts { get; }
    CameraLayout? CurrentLayout { get; set; }
    CameraLayout? SelectedStartupLayout { get; }
    int CameraCount { get; }
    int ConnectedCount { get; }
    string StatusText { get; }
    AtcSoft.CameraWall.Wpf.UserControls.CameraGrid? CameraGrid { get; }

    // Initialization
    void Initialize(AtcSoft.CameraWall.Wpf.UserControls.CameraGrid cameraGridControl);

    // Camera operations
    void AddCamera();
    void EditCamera(CameraConfiguration camera);
    void DeleteCamera(CameraConfiguration camera);
    void ShowFullScreen(CameraConfiguration camera);
    void RefreshAll();

    // Layout operations
    void CreateNewLayout();
    void DeleteCurrentLayout();
    void SetCurrentAsStartup();
    void SaveCurrentLayout();

    // Event handlers
    void OnConnectionStateChanged(CameraConnectionChangedEventArgs e);
    void OnPositionChanged(CameraPositionChangedEventArgs e);

    // Dialog operations
    void ShowAboutDialog();
    void ShowCheckForUpdatesDialog();
    void ShowSettingsDialog();

    // CanExecute properties
    bool CanCreateNewLayout { get; }
    bool CanDeleteCurrentLayout { get; }
    bool CanSetCurrentAsStartup { get; }
    bool CanRefreshAll { get; }
}
```

### IDialogService
Abstraction for showing dialogs:
```csharp
public interface IDialogService
{
    CameraConfiguration? ShowCameraConfigurationDialog(CameraConfiguration? camera, bool isNew);
    string? ShowInputBox(string title, string prompt, string defaultText = "");
    bool ShowConfirmation(string message, string title);
    void ShowError(string message, string title = "Error");
    void ShowInfo(string message, string title = "Information");
    void ShowAboutDialog();
    void ShowCheckForUpdatesDialog();
    void ShowFullScreenCamera(CameraConfiguration camera);
    void ShowSettingsDialog();
}
```

### ICameraStorageService
Abstraction for camera/layout persistence (JSON file implementation provided):
```csharp
public interface ICameraStorageService
{
    List<CameraConfiguration> GetAllCameras();
    CameraConfiguration? GetCameraById(Guid id);
    List<CameraLayout> GetAllLayouts();
    CameraLayout? GetLayoutById(Guid id);
    void AddOrUpdateCamera(CameraConfiguration camera);
    void DeleteCamera(Guid id);
    void AddOrUpdateLayout(CameraLayout layout);
    void DeleteLayout(Guid id);
    Guid? StartupLayoutId { get; set; }
    void Load();
    void Save();
}
```

### IApplicationSettingsService
Abstraction for application settings (theme, language, display, recording options):
```csharp
public interface IApplicationSettingsService
{
    GeneralSettings General { get; }
    CameraDisplayAppSettings CameraDisplay { get; }
    ConnectionAppSettings Connection { get; }
    PerformanceSettings Performance { get; }
    RecordingSettings Recording { get; }
    AdvancedSettings Advanced { get; }
    void Load();
    void Save();
}
```

### IRecordingService
Service for managing camera recording sessions:
```csharp
public interface IRecordingService
{
    event EventHandler<RecordingStateChangedEventArgs>? RecordingStateChanged;
    RecordingState GetRecordingState(Guid cameraId);
    bool StartRecording(CameraConfiguration camera, Player player);
    void StopRecording(Guid cameraId);
    void StopAllRecordings();
    bool IsRecording(Guid cameraId);
    bool TriggerMotionRecording(CameraConfiguration camera, Player player);
}
```

### IGitHubReleaseService
Service for checking GitHub releases for updates.

## MVVM Patterns

### Observable Properties
Use `[ObservableProperty]` attribute on private fields:
```csharp
[ObservableProperty]
private string displayName = string.Empty;
```

With callbacks:
```csharp
[ObservableProperty(AfterChangedCallback = nameof(OnSelectedCameraChanged))]
private CameraConfiguration? selectedCamera;

private static void OnSelectedCameraChanged()
    => CommandManager.InvalidateRequerySuggested();
```

With dependent properties:
```csharp
[ObservableProperty(DependentPropertyNames = [nameof(ScanButtonText)])]
private bool isScanning;
```

### Dependency Properties
Use `[DependencyProperty]` attribute on private fields for UserControls:
```csharp
[DependencyProperty(PropertyChangedCallback = nameof(OnCameraChanged))]
private CameraConfiguration? camera;

[DependencyProperty(DefaultValue = true)]
private bool autoSave = true;
```

### Relay Commands
Use `[RelayCommand]` attribute on methods:
```csharp
[RelayCommand(CanExecute = nameof(CanSave))]
private void Save() { ... }

private bool CanSave() => !string.IsNullOrWhiteSpace(Camera.DisplayName);
```

For async commands without "Async" suffix in command name:
```csharp
[RelayCommand("TestConnection", CanExecute = nameof(CanTestConnection))]
private async Task TestConnectionAsync() { ... }
```

### Command CanExecute Invalidation
When a property affects command CanExecute, use `AfterChangedCallback`:
```csharp
[ObservableProperty(AfterChangedCallback = nameof(OnIsTestingChanged))]
private bool isTesting;

private static void OnIsTestingChanged()
    => CommandManager.InvalidateRequerySuggested();
```

## UI Controls (Atc.Wpf)
Use Atc controls instead of standard WPF controls:
- `atc:GridEx` instead of `Grid`
- `atc:LabelTextBox` for labeled text inputs
- `atc:LabelComboBox` for labeled dropdowns (uses `IDictionary<string, string>` Items and `SelectedKey`)
- `atc:LabelPasswordBox` for password inputs
- `atc:LabelIntegerBox` for numeric inputs
- Every window and dialog has `atc:NiceWindow` as its root (or an Atc dialog box: `InfoDialogBox`, `QuestionDialogBox`, `InputDialogBox`, all `NiceDialogBox`), never a plain `Window`: a plain one does not follow the dark and light theme. A full-screen video window is a `NiceWindow` with `UseNoneWindowStyle="True"`, black whatever the theme; the walks check it still covers the taskbar. Only `VideoHost`'s hidden surface windows are plain

## Models
Models that need UI binding should inherit from `ObservableObject`:
```csharp
public partial class CameraConfiguration : ObservableObject
{
    [ObservableProperty]
    private string ipAddress = string.Empty;
}
```

## Enums
Use extension methods for enum conversions:
```csharp
public static string ToScheme(this CameraProtocol protocol)
    => protocol switch
    {
        CameraProtocol.Rtsp => CameraProtocol.Rtsp.ToStringLowerCase(),
        // ...
    };
```

### Camera Source vs. Protocol
`CameraProtocol` is the wire protocol for **network** cameras (RTSP / HTTP / HTTPS). `CameraSource` is the higher-level discriminator and selects between configuration shapes:

```csharp
public enum CameraSource
{
    Network = 0,  // uses Connection.IpAddress / Port / Protocol / Path
    Usb = 1,      // uses Connection.Usb (UsbConnectionSettings)
}
```

`ConnectionSettings.Source` defaults to `Network` so existing JSON-stored cameras keep working without migration. USB cameras populate `Connection.Usb` with `DeviceId` (the stable Media Foundation symbolic link), `FriendlyName`, optional `Format` (`UsbStreamFormat` — width × height × FPS × pixel-format), and `PreferAudio`. Use `CameraUriHelper.BuildSourceLocator(camera)` instead of `camera.BuildUri()` whenever the code path needs to handle both source kinds — `BuildUri()` throws on USB cameras to fail fast.

### Camera Connection Type
`ConnectionSettings.ConnectionType` (`CameraConnectionType`: `None` default, `Lan`, `Wifi`, `Usb`) is display-only: it drives the optional connection-type icon and never restarts a stream. Always read the displayed value through `CameraConnectionTypeHelper.GetEffective(connection)` — a USB source is always `Usb`, and a network camera never shows a stored `Usb`. Apply detected values with `CameraConnectionTypeHelper.ApplyDetected(current, detected, isExplicit)`: implicit detection only fills `None`, explicit (the user clicked Detect) may overwrite. Detection is ONVIF-based (`ICameraConnectionTypeDetector` / `OnvifConnectionTypeDetector`), never throws, and returns `None` when unknown. The icon's corner is stored *relative* to the info box (`ConnectionTypeIconPlacement`) and resolved with `CameraConnectionTypeHelper.ResolveIconCorner`, so it can never overlap the info box or the quick actions. See `docs/settings.md` (*Connection Type Options*).

## ViewModels
- Inherit from `ViewModelDialogBase` for dialog ViewModels
- Use `CloseRequested` event with `DialogClosedEventArgs` for dialog closing
- Subscribe to model `PropertyChanged` to react to changes

## Dialogs (in Library)
- `CameraConfigurationDialog` - Add/Edit camera with integrated network scanner
- `InputBox` - Simple text input dialog (for layout names)
- `SettingsDialog` - Configure general and display settings (uses extracted UserControls from `Dialogs/Parts/Settings/`)
- `CheckForUpdatesDialog` - Check for and display available updates from GitHub
- `RecordingsBrowserDialog` - Browse and play back recorded videos

## Windows (in Library)
- `FullScreenCameraWindow` - Display single camera in full screen mode
- `FullScreenRecordingWindow` - Play back recorded video in full screen mode

## Naming Conventions
- ViewModels: `{Name}ViewModel` (e.g., `CameraConfigurationDialogViewModel`)
- Dialogs: `{Name}Dialog` (e.g., `CameraConfigurationDialog`)
- Extensions: `{Type}Extensions` (e.g., `CameraProtocolExtensions`)
- UserControls: PascalCase (e.g., `CameraTile`, `CameraGrid`, `CameraOverlay`)
- Services: `I{Name}Service` / `{Name}Service` (e.g., `ICameraWallManager`, `CameraWallManager`)

## Dependency Injection
Library services are auto-registered using the source-generated extension method:
```csharp
// Auto-registers all services marked with [Registration] attribute
services.AddDependencyRegistrationsFromWpf();
```

For transitive registration of referenced assemblies:
```csharp
services.AddDependencyRegistrationsFromWpf(includeReferencedAssemblies: true);
```

## Atc.Source.Generators

### Service Registration Attribute
Services annotated with `[Registration]` are auto-registered via source-generated extension methods:
```csharp
[Registration(Lifetime.Singleton)]
public class CameraStorageService : ICameraStorageService { }

[Registration(Lifetime.Scoped)]
public class UserService : IUserService { }

[Registration(Lifetime.Transient)]
public class LoggerService { }
```

The generator creates `AddDependencyRegistrationsFrom{AssemblySuffix}()` extension methods. For `AtcSoft.CameraWall.Wpf`, this generates `AddDependencyRegistrationsFromWpf()`.

Advanced options:
```csharp
// Register as specific interface
[Registration(As = typeof(INotificationService))]
public class EmailService : INotificationService, IEmailService { }

// Register as both interface and self
[Registration(AsSelf = true)]
public class CacheService : ICacheService { }

// TryAdd pattern (only register if not already present)
[Registration(TryAdd = true)]
public class DefaultLogger : ILogger { }
```

### Options Binding
Use `[OptionsBinding]` for configuration classes with validation:
```csharp
[OptionsBinding("CameraWall", ValidateDataAnnotations = true, ValidateOnStart = true)]
public partial class CameraWallOptions
{
    [Required]
    public string ThemeBase { get; set; } = "Dark";
}
```

### DataAnnotations on Models
Use DataAnnotations for validation metadata:
```csharp
public partial class CameraConfiguration : ObservableObject
{
    [ObservableProperty]
    [Required(ErrorMessage = "IP Address is required")]
    private string ipAddress = string.Empty;

    [ObservableProperty]
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    private int port = 554;

    [ObservableProperty]
    [Required(ErrorMessage = "Display Name is required")]
    [StringLength(256, ErrorMessage = "Display Name cannot exceed 256 characters")]
    private string displayName = string.Empty;
}
```

## Settings Models

### GeneralSettings
```csharp
public class GeneralSettings
{
    public string ThemeBase { get; set; } = "Dark";           // "Dark" or "Light"
    public string ThemeAccent { get; set; } = "Blue";         // Accent color name
    public string Language { get; set; } = "1033";            // LCID as string (1033 = en-US)
    public bool ConnectCamerasOnStartup { get; set; } = true;
    public bool StartMaximized { get; set; } = false;
    public bool StartRibbonCollapsed { get; set; } = false;
}
```

### CameraDisplayAppSettings
```csharp
public class CameraDisplayAppSettings
{
    public bool ShowOverlayTitle { get; set; } = true;
    public bool ShowOverlayDescription { get; set; } = true;
    public bool ShowOverlayTime { get; set; } = false;
    public bool ShowOverlayConnectionStatus { get; set; } = true;
    public bool ShowOverlayQuickActions { get; set; } = true;
    public bool ShowOverlayConnectionTypeIcon { get; set; }                    // opt-in LAN / Wi-Fi / USB icon
    public ConnectionTypeIconPlacement ConnectionTypeIconPlacement { get; set; } = ConnectionTypeIconPlacement.SameRow;  // relative to the info box
    public double OverlayOpacity { get; set; } = 0.7;
    public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.TopLeft;
    public bool AllowDragAndDropReorder { get; set; } = true;
    public bool AutoSaveLayoutChanges { get; set; } = true;
    public string SnapshotPath { get; set; } = ApplicationPaths.DefaultSnapshotsPath;
}
```

### RecordingSettings
```csharp
public class RecordingSettings
{
    public string RecordingPath { get; set; } = ApplicationPaths.DefaultRecordingsPath;
    public string RecordingFormat { get; set; } = "mp4";
    public bool EnableRecordingOnMotion { get; set; }
    public bool EnableRecordingOnConnect { get; set; }
    public bool UseDailySubfolders { get; set; } = true;        // recordings\{camera}\yyyy-MM-dd\...
    public bool SeparateThumbnailFolder { get; set; } = true;   // recordings\thumbnails\{camera}\...
    public MotionDetectionSettings MotionDetection { get; set; } = new();
}
```

### Recording paths
`RecordingPathHelper` (Core) is the only place that knows where recordings and thumbnails live. Never build a recording path, sanitise a camera folder name, or derive a thumbnail with `Path.ChangeExtension(video, ".png")` by hand. Writers call `BuildRecordingPath` / `BuildThumbnailPath` with `RecordingLayoutOptions.From(settings)`. Readers ignore the layout settings: they take the camera from `GetCameraFolderFromPath`, the time from `TryParseTimestamp`, and the thumbnail from `FindThumbnailPath` / `GetThumbnailCandidates`, so archives written under any layout keep working. Everything that belongs to a recording (thumbnail, the activity index `{video}.activity.jsonl`, the preview pictures `{video}.previews.jpg` with `{video}.previews.json`, a person's review `{video}.review.json`) is listed by `GetCompanionPaths`: delete, protect and move through it, and add a new kind of companion file there. The folder must always follow the settings: when the layout options change, call `RecordingLayoutReorganizer.Synchronize` (it restarts running recordings that are not in place, then moves the files).

### FFmpeg
The engine needs exactly FFmpeg 9 (`Flyleaf.FFmpeg.Bindings` 9.0.2 load one major version of each library). `FFmpegRuntime` knows the versions and where to look; `FFmpegRuntime.Check()` tells Ready, WrongVersion or Missing with what was found. Check before `VideoEngineBootstrap.Initialize`: the desktop apps show `FFmpegSetup.ShowDialog(check)` (install into `FFmpegRuntime.ManagedFolder`, or a folder the user names), the server logs and exits unless started with `--install-ffmpeg` (`ServerFFmpegStartup`). The pinned build is in three places that must agree: `FFmpegRuntime.ReleaseVersion`, `FFmpegInstaller.Package` (URL and SHA-256) and `release-please.yml`. FFmpeg 9 verifies TLS certificates by default; the engine passes `tls_verify=0` unless `StreamOptions.VerifyTlsCertificate`, and so does the server's HLS command for HTTPS cameras. `ATCSOFT_FFMPEG_SEARCH=explicit` limits the search so the E2E tests can show the dialog.

### Disk space
`DiskSpaceGuard` (Core, one instance per app, shared by the recording and cleanup services) keeps `MinFreeSpaceMb` free on every volume that holds media: the global recording folder (or its default), each camera's own folder and the snapshot folder. It deletes the oldest media first and reports per volume (fine, reclaimed, still short, unreadable). A recording starts only when its folder's volume is fine or reclaimed; a volume still short stops its recordings; an unreadable one refuses new recordings but lets running ones go on. Free space is read through `IFreeSpaceReader` (tests use a fake; never `DriveInfo` directly). A recording whose writes fail reports `RecordingError` and is stopped. A new place the apps write media to joins the guard's request. See `docs/disk-space-guard.md`.

### Rotation
`CameraDisplaySettings.Rotation` (clockwise) must reach every consumer of the picture: `VideoPlayer.CaptureFrameAsync` always returns the rotated picture (GPU snapshot or CPU fallback), software frames are rotated in `CpuFrameUploader`, and recordings are flagged with a display matrix (`DisplayMatrix`, read back by `MediaProbe.GetRotation`) because the recorder copies packets. Anything that maps coordinates onto the video must use the displayed size (`MotionBoxMapper.GetDisplaySize`), not `StreamInfo.Width/Height`. Call `ApplyRotation` whenever a `CameraTile` gets a new player.

### AdvancedSettings
```csharp
public class AdvancedSettings
{
    public bool EnableDebugLogging { get; set; }
    public string LogPath { get; set; } = ApplicationPaths.DefaultLogsPath;
}
```

## Threading
Use `SemaphoreSlim` for thread-safe operations with proper acquisition tracking:
```csharp
var acquired = false;
try
{
    semaphore.Wait();
    acquired = true;
    // work...
}
finally
{
    if (acquired)
    {
        semaphore.Release();
    }
}
```

## Converters
- `BoolToOpacityConverter` - Converts bool to opacity value (0.0-1.0)
- `ConnectionStateToColorConverter` - Converts ConnectionState enum to brush color
- `ConnectionStateToTextConverter` - Converts ConnectionState enum to localized text
- `OverrideOrDefaultMultiValueConverter` - Returns override value if non-null, otherwise default value

## Factories
Use `DropDownItemsFactory` for centralized dropdown items and defaults:
```csharp
// Dropdown items for comboboxes
public static IDictionary<string, string> VideoQualityItems { get; }
public static IDictionary<string, string> RtspTransportItems { get; }
public static IDictionary<string, string> RecordingFormatItems { get; }
public static IDictionary<string, string> OverlayPositionItems { get; }
public static IDictionary<string, string> OverlayOpacityItems { get; }  // 0%-100% in 10% increments
public static IDictionary<string, string> ProtocolItems { get; }

// Default values
public const string DefaultVideoQuality = "Auto";
public const string DefaultRtspTransport = "tcp";
public const string DefaultRecordingFormat = "mp4";
public const string DefaultOverlayPosition = "TopLeft";
public const string DefaultOverlayOpacity = "0.7";
public const string DefaultProtocol = "Rtsp";

// Utility methods
public static int GetMaxResolutionFromQuality(string videoQuality);
```

## Helpers

### ApplicationPaths
Provides default application paths using `Environment.SpecialFolder.CommonApplicationData`:
```csharp
public static class ApplicationPaths
{
    public static string DefaultLogsPath { get; }       // {ProgramData}\AtcSoft\CameraWall\logs
    public static string DefaultSnapshotsPath { get; }  // {ProgramData}\AtcSoft\CameraWall\snapshots
    public static string DefaultRecordingsPath { get; } // {ProgramData}\AtcSoft\CameraWall\recordings
    public static string DefaultSettingsPath { get; }   // {ProgramData}\AtcSoft\CameraWall\settings.json
    public static string DefaultCameraDataPath { get; } // {ProgramData}\AtcSoft\CameraWall\cameras.json
}
```

## Debug Logging
Debug logging is configured via Serilog in `App.xaml.cs`:
- Controlled by `AdvancedSettings.EnableDebugLogging` and `AdvancedSettings.LogPath`
- When enabled, logs to `{LogPath}\camera-wall-{date}.log`
- Daily rolling with 7-day retention
- Settings loaded early (before Host builder) to configure logging at startup
- Logs include: layout loading, camera connection state changes, recording state changes

## Build
```bash
dotnet build
dotnet run --project src/AtcSoft.CameraWall.Wpf.App
```
