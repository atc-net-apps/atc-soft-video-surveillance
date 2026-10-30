# 🏛️ AtcSoft.VideoSurveillance Architecture

A multi-assembly video surveillance platform supporting both a WPF desktop application and a headless REST API + Blazor Web UI, sharing a common Core library. Built on .NET 10.0 with in-process FFmpeg via AtcSoft.VideoEngine.

## 🌐 High-Level Architecture

```mermaid
graph TB
    subgraph Clients["Client Applications"]
        WPF["AtcSoft.CameraWall.Wpf.App<br/><i>WPF Desktop (Standalone)</i>"]
        VSWpf["AtcSoft.VideoSurveillance.Wpf.App<br/><i>WPF Desktop (API Client)</i>"]
        Blazor["AtcSoft.VideoSurveillance.Blazor.App<br/><i>Blazor WebAssembly</i>"]
    end

    subgraph Server["Server"]
        API["AtcSoft.VideoSurveillance.Api<br/><i>ASP.NET Core Host</i>"]
    end

    subgraph Orchestration
        Aspire["AtcSoft.VideoSurveillance.Aspire<br/><i>.NET Aspire AppHost</i>"]
    end

    VSWpf -- "REST + events" --> API
    Blazor -- "REST + events" --> API
    Aspire -- "orchestrates" --> API
    Aspire -- "orchestrates" --> Blazor
    Aspire -- "orchestrates" --> VSWpf

    WPF -. "standalone desktop<br/>(no API dependency)" .-> Cameras["RTSP/HTTP Cameras"]
    API -- "RTSP/HTTP" --> Cameras
```

## 🔗 Assembly Dependency Graph

```mermaid
graph BT
    Core["AtcSoft.VideoSurveillance.Core<br/><i>net10.0</i>"]
    VE["AtcSoft.VideoEngine<br/><i>net10.0</i>"]
    DX["AtcSoft.VideoEngine.DirectX<br/><i>net10.0-windows</i>"]
    VW["AtcSoft.VideoEngine.Windows<br/><i>net10.0-windows</i>"]
    VP["AtcSoft.VideoPlayer.Wpf<br/><i>net10.0-windows</i>"]
    CW["AtcSoft.CameraWall.Wpf<br/><i>net10.0-windows</i>"]
    App["AtcSoft.CameraWall.Wpf.App<br/><i>net10.0-windows</i>"]
    VSWpf["VideoSurveillance.Wpf<br/><i>net10.0-windows</i>"]
    VSApp["VideoSurveillance.Wpf.App<br/><i>net10.0-windows</i>"]
    Contracts["Api.Contracts<br/><i>net10.0</i>"]
    Domain["Api.Domain<br/><i>net10.0</i>"]
    API["Api<br/><i>net10.0-windows</i>"]
    Blazor["Blazor.App<br/><i>net10.0</i>"]
    Aspire["Aspire<br/><i>net10.0</i>"]

    DX --> VE
    VP --> DX
    VW --> Core
    CW --> Core
    CW --> VP
    App --> CW
    App --> VW
    VSWpf --> Core
    VSWpf --> VP
    VSApp --> VSWpf
    Contracts --> Core
    Domain --> Contracts
    Domain --> Core
    API --> Domain
    API --> Contracts
    API --> Core
    API --> VE
    API --> VW
    Aspire --> API
    Aspire --> Blazor
    Aspire --> VSApp
```

`AtcSoft.VideoEngine.Windows` is the optional Windows-only USB layer — Media Foundation device enumeration plus a WMI hot-plug watcher. The Core abstraction (`IUsbCameraEnumerator` / `IUsbCameraWatcher`) lives in `AtcSoft.VideoSurveillance.Core` so non-Windows hosts can compose with `NullUsbCameraEnumerator` / `NullUsbCameraWatcher` and a future V4L2 / AVFoundation implementation can drop in without touching consumers. The `Api` host targets `net10.0-windows` so it can reference `VideoEngine.Windows` directly; Linux and macOS USB support is deferred (it is not on the roadmap).

## 📊 Layered Architecture

```mermaid
graph TB
    subgraph Presentation["Presentation Layer"]
        direction LR
        AppShell["CameraWall.Wpf.App<br/><i>Fluent.Ribbon, Serilog</i>"]
        VSAppShell["VideoSurveillance.Wpf.App<br/><i>Fluent.Ribbon, Serilog</i>"]
        BlazorUI["Blazor WebAssembly<br/><i>MudBlazor, event stream reader</i>"]
    end

    subgraph Library["WPF Library Layer"]
        direction LR
        CWLib["AtcSoft.CameraWall.Wpf<br/><i>Dialogs, Services, UserControls</i>"]
        VSLib["VideoSurveillance.Wpf<br/><i>GatewayService, HubService</i>"]
    end

    subgraph VideoLayer["Video Layer"]
        direction LR
        VideoPlayer["Wpf.VideoPlayer<br/><i>VideoHost + DComp Overlay</i>"]
        DirectX["VideoEngine.DirectX<br/><i>D3D11VA, SwapChain</i>"]
        VideoEngine["VideoEngine<br/><i>FFmpeg Demux/Decode/Record</i>"]
    end

    subgraph APILayer["API Layer"]
        direction LR
        APIHost["Api Host<br/><i>Kestrel, GET /events, HLS</i>"]
        DomainHandlers["Api.Domain<br/><i>Handler Implementations</i>"]
        ContractsGen["Api.Contracts<br/><i>Generated from OpenAPI</i>"]
    end

    subgraph CoreLayer["Core Layer"]
        Core["AtcSoft.VideoSurveillance.Core<br/><i>Models, Services, Events, Helpers</i>"]
    end

    AppShell --> CWLib
    VSAppShell --> VSLib
    CWLib --> VideoPlayer
    VSLib --> VideoPlayer
    CWLib --> Core
    VideoPlayer --> DirectX
    DirectX --> VideoEngine
    BlazorUI -- "HTTP + Server-Sent Events" --> APIHost
    APIHost --> DomainHandlers
    DomainHandlers --> ContractsGen
    APIHost --> Core
    APIHost --> VideoEngine
    ContractsGen --> Core
```

## 🎬 Video Pipeline Architecture

```mermaid
graph LR
    Camera["Camera<br/><i>RTSP/HTTP Stream</i>"]

    subgraph VideoEngine["AtcSoft.VideoEngine"]
        Demuxer["Demuxer<br/><i>Packet extraction</i>"]
        Decoder["VideoDecoder<br/><i>CPU or D3D11VA</i>"]
        Remuxer["Remuxer<br/><i>Recording to file</i>"]
        Capture["FrameCapture<br/><i>PNG snapshots</i>"]
    end

    subgraph DirectX["VideoEngine.DirectX"]
        HwAccel["HwAccelContext<br/><i>D3D11VA device</i>"]
        VPR["VideoProcessorRenderer<br/><i>NV12 → BGRA + per-stream rotation</i>"]
        SCP["SwapChainPresenter<br/><i>DirectComposition</i>"]
    end

    subgraph WPF["Wpf.VideoPlayer"]
        VideoHost["VideoHost<br/><i>DComp Surface + Overlay</i>"]
    end

    Camera --> Demuxer
    Demuxer --> Decoder
    Decoder --> Remuxer
    Decoder --> Capture
    Decoder --> HwAccel
    HwAccel --> VPR
    VPR --> SCP
    SCP --> VideoHost
```

## 📝 OpenAPI-First API Design

```mermaid
graph LR
    YAML["VideoSurveillance.yaml<br/><i>OpenAPI 3.2 Spec</i>"]

    subgraph ServerSide["Server-Side Generation"]
        Contracts["Api.Contracts<br/><i>DTOs, Handler Interfaces</i>"]
        Domain["Api.Domain<br/><i>Handler Implementations</i>"]
    end

    subgraph ClientSide["Client-Side Generation"]
        BlazorClient["Blazor.App<br/><i>Typed API Client</i>"]
        WpfClient["VideoSurveillance.Wpf<br/><i>Typed API Client</i>"]
    end

    YAML -- "atc-rest-api-source-generator<br/>(server)" --> Contracts
    YAML -- "atc-rest-api-source-generator<br/>(client)" --> BlazorClient
    YAML -- "atc-rest-api-source-generator<br/>(client)" --> WpfClient
    Contracts --> Domain
```

## ⚡ Real-Time Event Flow

```mermaid
sequenceDiagram
    participant Camera as RTSP Camera
    participant API as Api Server
    participant Feed as GET /events
    participant Blazor as Blazor Client
    participant WPF as WPF Client

    Blazor ->> Feed: GET /events (Last-Event-ID)
    WPF ->> Feed: GET /events (Last-Event-ID)

    Camera ->> API: Video stream
    API ->> API: Motion detection
    API ->> Feed: motionDetected
    Feed ->> Blazor: event (id, data)
    Feed ->> WPF: event (id, data)

    API ->> API: Recording state change
    API ->> Feed: recordingStateChanged
    Feed ->> Blazor: event
    Feed ->> WPF: event

    API ->> API: Connection state change
    API ->> Feed: connectionStateChanged
    Feed ->> Blazor: event
    Feed ->> WPF: event
```

## 📦 Assembly Responsibilities

### 📚 Core Layer

| Assembly | Target | Description |
|----------|--------|-------------|
| **AtcSoft.VideoSurveillance.Core** | net10.0 | Shared domain library with zero UI dependencies. Contains all models (camera configuration, layouts, settings, overrides, recording entries), enums (ConnectionState, RecordingState, CameraProtocol, etc.), events (motion detected, recording state changed, connection changed), service interfaces (ICameraStorageService, IApplicationSettingsService, IRecordingService, IMotionDetectionService, ITimelapseService, IMediaCleanupService, etc.), helpers (ApplicationPaths, CameraUriHelper, RecordingPolicyHelper), and factories (DropDownItemsFactory). |

### 🎬 Video Engine Layer

| Assembly | Target | Description |
|----------|--------|-------------|
| **AtcSoft.VideoEngine** | net10.0 | Cross-platform video engine using in-process FFmpeg via Flyleaf.FFmpeg.Bindings. Provides `Demuxer` for packet extraction, `VideoDecoder` for CPU/GPU decoding, `Remuxer` for recording to file (MP4/MKV) — with optional `rotate` metadata for rotated streams — `FrameCapture` for PNG snapshots, and `MediaProbe` for stream metadata. Exposes `IVideoPlayer` and `IVideoPlayerFactory` interfaces. Defines `IGpuAccelerator` for pluggable hardware acceleration and a `VideoRotation` enum for per-stream rotation. |
| **AtcSoft.VideoEngine.DirectX** | net10.0-windows | Windows-specific GPU acceleration using Direct3D 11. Implements `IGpuAccelerator` via `D3D11Accelerator` for D3D11VA hardware-accelerated decoding. Provides `VideoProcessorRenderer` (NV12 → BGRA, with optional 0/90/180/270° rotation via `VideoProcessorSetStreamRotation`), `SwapChainPresenter` (DirectComposition swap chain rendering), `HwAccelContext` (FFmpeg hardware device setup), and `GpuSnapshotCapture` (GPU-surface PNG capture). Uses Vortice bindings. |

### 🖥️ WPF Presentation Layer

| Assembly | Target | Description |
|----------|--------|-------------|
| **AtcSoft.VideoPlayer.Wpf** | net10.0-windows | WPF `VideoHost` control that displays video via DirectComposition surface with XAML overlay support. Uses native window hierarchy (Surface Window + Overlay Window as WS_CHILD). Provides `OverlayBridge` for connecting WPF XAML elements to the native overlay window. |
| **AtcSoft.CameraWall.Wpf** | net10.0-windows | WPF library containing the complete camera wall implementation (not published to NuGet; consumed by the CameraWall app). Includes: `CameraWallManager` facade, service implementations (storage, settings, recording, motion detection, timelapse, media cleanup, segmentation, thumbnails, GitHub updates), 7+ dialog windows (CameraConfiguration, Settings, RecordingsBrowser, AssignCamera, CheckForUpdates, About, InputBox), 38+ dialog part UserControls for settings/configuration, camera grid and tile UserControls, motion bounding box overlay, and localization resources (en-US, da-DK, de-DE). |
| **AtcSoft.CameraWall.Wpf.App** | net10.0-windows | Thin shell WPF application. Provides `MainWindow` with Fluent.Ribbon UI (Layouts, Cameras, View, Help tabs). Configures `Microsoft.Extensions.Hosting`, Serilog file logging (daily rolling, 7-day retention), and DI registration via `AddDependencyRegistrationsFromCameraWall()`. |
| **AtcSoft.VideoSurveillance.Wpf** | net10.0-windows | WPF library for the VideoSurveillance API client. Provides `GatewayService` (OpenAPI-generated REST client split by resource: Cameras, Layouts, Recordings, Settings) and `SurveillanceEventService` (the `GET /events` reader for real-time events). Uses `Atc.Rest.Api.SourceGenerator` for typed API client generation from `VideoSurveillance.yaml`. |
| **AtcSoft.VideoSurveillance.Wpf.App** | net10.0-windows | Thin shell WPF application for the VideoSurveillance API client. Provides `MainWindow` with Fluent.Ribbon UI (Home tab with Cameras, Layouts, Recordings buttons). Status bar shows the server URL and whether the live updates are on. Configures `Microsoft.Extensions.Hosting`, Serilog, splash screen, and DI for `GatewayService` + `SurveillanceEventService`. Reads `ApiBaseAddress` from `appsettings.json`. |

### 🌍 Server Layer

| Assembly | Target | Description |
|----------|--------|-------------|
| **AtcSoft.VideoSurveillance.Api.Contracts** | net10.0 | Auto-generated from `VideoSurveillance.yaml` via atc-rest-api-source-generator. Contains request/response DTOs, handler interfaces (IListCamerasHandler, ICreateCameraHandler, etc.), and the host wiring (`AddVideoSurveillanceApi()` / `MapVideoSurveillanceApi()`). Errors are RFC 9457 ProblemDetails. Shared contract between server and client. |
| **AtcSoft.VideoSurveillance.Api.Domain** | net10.0 | Handler implementations for all API endpoints. 16 handlers covering Cameras CRUD (8), Layouts CRUD + Apply (5), Recordings (1), Settings (2). Includes mapping extensions for domain-to-DTO conversions. |
| **AtcSoft.VideoSurveillance.Api** | net10.0 | ASP.NET Core host application. Registers all services (JsonCameraStorageService, JsonApplicationSettingsService, ServerRecordingService, ServerMotionDetectionService, VideoPlayerFactory, StreamingService). Configures CORS, the camera events at `GET /events`, live streams on a lease (`POST /cameras/{id}/stream`), static file serving for HLS streams (`/streams`) and recordings (`/recordings-files`), OpenAPI docs via Scalar. Runs `CameraConnectionService` and `SurveillanceEventBroadcaster` as hosted services. |

### 🌐 Web UI

| Assembly | Target | Description |
|----------|--------|-------------|
| **AtcSoft.VideoSurveillance.Blazor.App** | net10.0 | Blazor WebAssembly client with MudBlazor UI. Pages: Dashboard (live stats), Cameras (CRUD + snapshot/record), Layouts (drag-drop grid editor), Live View (HLS streaming with motion bounding boxes), Recordings (browse/playback/download), Settings (7-tab configuration). Uses `GatewayService` for API calls and `EventStreamService` for real-time updates from `GET /events`. Auto-generated API client from OpenAPI spec. |

### ☁️ Orchestration

| Assembly | Target | Description |
|----------|--------|-------------|
| **AtcSoft.VideoSurveillance.Aspire** | net10.0 | .NET Aspire AppHost for distributed orchestration. Starts the API on port 5000, then the Blazor.App and Wpf.App with API service references. Provides Aspire dashboard for monitoring, logs, traces, and health checks. |

### ✅ Testing

| Assembly | Target | Description |
|----------|--------|-------------|
| **AtcSoft.VideoSurveillance.Core.Tests** | net10.0 | xUnit v3 unit tests for Core library. Tests models, enums, events, extensions, factories, and helpers. Uses AutoFixture, AwesomeAssertions, and NSubstitute. |
| **AtcSoft.VideoEngine.Tests** | net10.0 | xUnit v3 unit tests for VideoEngine. Tests FFmpeg integration, video player, and frame capture. |
| **AtcSoft.VideoSurveillance.Api.Tests** | net10.0 | xUnit v3 unit tests for API handlers and domain logic. Tests CRUD operations and mapping extensions. |

### 📦 Installer

| Assembly | SDK | Description |
|----------|-----|-------------|
| **AtcSoft.VideoSurveillance.Installer** | WixToolset.Sdk 5.0.2 | WiX MSI installer for the desktop application. Auto-harvests published binaries via WiX Heat. Produces x64 Windows Installer package with Start Menu and Desktop shortcuts. Built via command line only (excluded from Visual Studio solution). |

## 🛠️ Key Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 10.0 / C# 14.0 |
| Desktop UI | WPF with Fluent.Ribbon, Atc.Wpf.Controls |
| Web UI | Blazor WebAssembly with MudBlazor |
| Server | ASP.NET Core, Server-Sent Events, HLS |
| API Definition | OpenAPI 3.2 (atc-rest-api-source-generator v3) |
| Video Engine | AtcSoft.VideoEngine (in-process FFmpeg via Flyleaf.FFmpeg.Bindings) |
| GPU Acceleration | Direct3D 11 (D3D11VA) via Vortice bindings |
| Rendering | DirectComposition swap chain |
| Orchestration | .NET Aspire |
| MVVM | Atc.XamlToolkit (source generators) |
| DI Registration | Atc.SourceGenerators (`[Registration]` attribute) |
| Theming | Atc.Wpf.Theming |
| Logging | Serilog (console + file sink) |
| Testing | xUnit v3, AutoFixture, AwesomeAssertions, NSubstitute |
| Versioning | release-please (Conventional Commits) |
| Code Analysis | StyleCop, Meziantou, SonarAnalyzer, SecurityCodeScan |
| Installer | WiX Toolset v5 |

## 💾 Data Storage

```mermaid
graph LR
    subgraph ProgramData["%ProgramData%/AtcSoft/CameraWall/"]
        cameras["cameras.json<br/><i>Camera configs & layouts</i>"]
        settings["settings.json<br/><i>Application settings</i>"]
        logs["logs/<br/><i>Serilog log files</i>"]
        snapshots["snapshots/<br/><i>Camera snapshots & timelapse</i>"]
        recordings["recordings/<br/><i>Video recordings + thumbnails<br/>(optional day folders, thumbnails/)</i>"]
    end
```

Both the WPF app and the API server use the same JSON-based storage format under `%ProgramData%\AtcSoft\CameraWall\`. The `ApplicationPaths` helper in Core provides default paths.

## 🔀 Per-Camera Override System

Every application-level setting can be overridden on a per-camera basis. Override models use nullable properties where `null` means "use application default":

```mermaid
graph TB
    AppSettings["Application Settings<br/><i>Global defaults</i>"]
    CameraOverrides["Camera Overrides<br/><i>Nullable properties</i>"]
    Effective["Effective Value<br/><i>GetEffectiveValue()</i>"]

    AppSettings --> Effective
    CameraOverrides --> Effective
```

Override categories: Connection, CameraDisplay, Performance, Recording, MotionDetection (with nested BoundingBox).

## 🔧 Service Registration

Services are auto-registered using `[Registration]` source-generated attributes:

- **WPF**: `services.AddDependencyRegistrationsFromCameraWall(includeReferencedAssemblies: true)` registers all services from the CameraWall library and referenced Core assembly.
- **API**: Explicit singleton registration in `Program.cs` for server-specific service implementations (JsonCameraStorageService, ServerRecordingService, etc.).
