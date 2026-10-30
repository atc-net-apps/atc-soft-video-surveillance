# 📹 AtcSoft.VideoSurveillance

A professional video surveillance platform for live monitoring of multiple RTSP/HTTP camera streams. Includes a WPF desktop application with an intuitive ribbon interface, a headless REST API server edition (live events over Server-Sent Events), and Aspire orchestration.

[![Release](https://img.shields.io/github/v/release/davidkallesen/AtcSoft.VideoSurveillance?include_prereleases)](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/releases)
[![NuGet](https://img.shields.io/nuget/v/AtcSoft.VideoSurveillance.Wpf.Core)](https://www.nuget.org/packages/AtcSoft.VideoSurveillance.Wpf.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## ✨ Features

### 🖥️ Camera Display
- **Dynamic Grid Layout** - Auto-calculates optimal grid based on camera count (optimized for 4:3 aspect ratio)
- **Camera Tile Overlay** - Configurable overlay showing title, description, timestamp, and connection status
- **Overlay Configuration** - Choose corner position (TopLeft, TopRight, BottomLeft, BottomRight) and opacity (0-100%)
- **Stream Rotation** - Per-camera 0°/90°/180°/270° rotation for the live picture, motion boxes, snapshots and thumbnails. Recordings carry a rotation flag (display matrix) that standard players and the built-in player apply at playback.
- **Full Screen Mode** - Double-click or use context menu to view any camera in full screen (Escape to exit)
- **Connection State Indicators** - Visual indicators for Connected, Connecting, and Error states

### 🔍 Zoom & Pan
- **Scroll Zoom** - Ctrl+Scroll to zoom in/out on any camera tile or full screen view, centered on mouse pointer
- **Rectangle Selection Zoom** - Ctrl+Left-drag to draw a selection rectangle, zooms to the selected region on release
- **Pan** - Ctrl+Left-drag while zoomed to pan the viewport
- **Zoom Indicator** - "Zoom: 150%" badge displayed on camera tile while zoomed
- **Reset Zoom** - Double-click or ESC to reset back to fit-to-view
- **Single Zoom Policy** - Only one camera tile can be zoomed at a time in the grid layout; zooming another resets the previous
- **Motion Overlay Pause** - Bounding box overlays are hidden while zoomed to avoid visual clutter, restored on reset
- **GPU-Accelerated** - Zoom operates at the DirectComposition transform level for smooth, low-latency rendering

### 📷 Camera Management
- **Context Menu** - Edit, Delete, Full Screen, Swap Left/Right, Snapshot, Reconnect, Start/Stop Recording, Reset Zoom, Rotate (0°/90°/180°/270°)
- **Drag-and-Drop** - Reorder cameras by dragging tiles within the grid
- **Network Scanner** - Auto-discover cameras on the local network (integrated in Add Camera dialog)
- **Test Connection** - Validate camera connectivity before saving
- **Multiple Protocols** - RTSP, HTTP, and HTTPS support with configurable ports and paths
- **USB / Webcam Support** - Mix DirectShow / UVC USB cameras with IP cameras in the same grid (Windows). Devices are enumerated via Media Foundation; hot-plug events surface via WMI so unplugged devices transition cleanly to a `DeviceUnplugged` state instead of thrashing in a reconnect loop. See [`docs/usb-cameras.md`](docs/usb-cameras.md).
- **Per-Camera Overrides** - Override any application-level setting on a per-camera basis (connection, display, performance, recording, motion detection)

### 🔴 Recording
- **Manual Recording** - Start/stop recording per camera via context menu
- **Auto-Record on Connect** - Automatically start recording when a camera connects
- **Motion-Triggered Recording** - Automatically record when motion is detected, with configurable post-motion duration
- **Recording Segmentation** - Clock-aligned automatic file segmentation (e.g., every 15 minutes at :00, :15, :30, :45)
- **Recording Format** - Configurable format (MP4, MKV, AVI) with per-camera override
- **Recording Indicator** - Visual indicator when recording is active
- **Thumbnail Generation** - Auto-generates thumbnails from recording frames (single image or 2x2 grid)
- **Recording Folder Layout** - Optional folder per day and a separate thumbnail folder, to keep long-running archives easy to browse
- **Recordings Browser** - Browse, filter, and playback recorded videos with playback overlay

### 🏃 Motion Detection
- **Frame-Based Detection** - Compares consecutive frames using grayscale pixel differencing
- **Multi-Bounding Box** - Detects and highlights multiple motion regions simultaneously using grid-based clustering
- **Configurable Sensitivity** - 0-100 scale (higher = more motion required to trigger)
- **Analysis Resolution** - Configurable analysis width/height for performance tuning
- **Bounding Box Visualization** - Real-time colored bounding boxes in both grid and full-screen views with configurable color, thickness, smoothing, and minimum area
- **Smart Scheduling** - Staggered analysis across cameras to prevent CPU spikes
- **Post-Motion Duration** - Continue recording for configurable seconds after motion stops
- **Cooldown** - Configurable delay before motion can trigger a new recording

### ⏱️ Timelapse
- **Interval Capture** - Automatically capture snapshots at configurable intervals (10s, 1m, 5m, 1h)
- **Per-Camera Configuration** - Enable/disable and configure interval per camera
- **Organized Storage** - Snapshots stored in camera-named subdirectories

### 📐 Layout Management
- **Named Layouts** - Create, save, and switch between named layouts
- **Startup Layout** - Designate a layout to load on application start
- **Auto-Save** - Automatic persistence of camera positions (configurable)
- **Camera Assignment** - Dual-list dialog for assigning cameras to layouts

### 🧹 Media Cleanup
- **Automatic Cleanup** - Remove old recordings and snapshots based on retention policy
- **Flexible Scheduling** - On startup, hourly, daily, or weekly cleanup
- **Configurable Retention** - Set maximum age for recordings (default 30 days) and snapshots (default 7 days)

### 🔌 Connectivity
- **Auto-Reconnect** - Automatic reconnection on failure with configurable delay
- **Connection Timeout** - Configurable timeout per camera or globally
- **Disconnect Notifications** - Optional notifications on camera disconnect/reconnect
- **Notification Sounds** - Optional audio alert on connectivity events

### 🎨 User Interface
- **Fluent Ribbon UI** - Modern ribbon interface with Layouts, Cameras, View, and Help tabs
- **Theme Support** - Dark/Light theme switching with configurable accent colors
- **Multi-Language** - Localization support via LCID-based language selection
- **Settings Dialog** - Comprehensive settings organized into General, Camera Display, Connection, Performance, Recording, Motion Detection, Timelapse, and Advanced sections
- **Recordings Browser** - Browse and playback recorded videos with filtering
- **About Dialog** - Application version and information
- **Check for Updates** - GitHub-based update checking with download link

### 📦 Installation
- **MSI Installer** - Windows installer with Start Menu and Desktop shortcuts
- **Per-Machine Install** - Installs for all users on the machine

## 🛠️ Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 10.0 / .NET 10.0-windows |
| Desktop UI | WPF with Fluent.Ribbon, Atc.Wpf.Controls |
| Server | ASP.NET Core, Server-Sent Events, HLS |
| API Definition | OpenAPI 3.2 (atc-rest-api-source-generator v3) |
| Video Engine | AtcSoft.VideoEngine (desktop + server, in-process FFmpeg) |
| Orchestration | .NET Aspire |
| MVVM | Atc.XamlToolkit |
| Source Generators | Atc.SourceGenerators |
| Theming | Atc.Wpf.Theming |
| Logging | Serilog (file sink) |
| Versioning | release-please (Conventional Commits) |
| Installer | WiX Toolset v5 |

## 🏗️ Solution Structure

```
AtcSoft.VideoSurveillance/
├── src/
│   ├── AtcSoft.VideoSurveillance.Core/        # 📚 Shared library: models, enums, events, service interfaces
│   ├── AtcSoft.VideoEngine/                   # 🎬 Cross-platform video engine (FFmpeg in-process)
│   ├── AtcSoft.VideoEngine.DirectX/           # 🖥️ D3D11VA GPU acceleration for WPF
│   ├── AtcSoft.VideoPlayer.Wpf/               # 🎞️ WPF VideoHost control (DComp surface + overlay)
│   ├── AtcSoft.VideoSurveillance.Wpf.Core/    # 🧩 Shared WPF library (dialogs, controls, services)
│   ├── AtcSoft.CameraWall.Wpf/                # 🧱 CameraWall WPF library (used by the CameraWall app)
│   ├── AtcSoft.CameraWall.Wpf.App/            # 🚀 Standalone WPF desktop app (Fluent.Ribbon)
│   ├── AtcSoft.VideoSurveillance.Wpf/         # 🌐 WPF library for API client (GatewayService, event stream)
│   ├── AtcSoft.VideoSurveillance.Wpf.App/     # 🚀 WPF API client app (Fluent.Ribbon)
│   ├── VideoSurveillance.yaml                  # 📝 OpenAPI 3.2 spec (shared contract)
│   ├── AtcSoft.VideoSurveillance.Api.Contracts/ # 📋 Generated: endpoints, models, handler interfaces
│   ├── AtcSoft.VideoSurveillance.Api.Domain/  # ⚙️ Handler implementations calling Core services
│   ├── AtcSoft.VideoSurveillance.Api/         # 🌍 ASP.NET Core host (REST, events, HLS)
│   ├── AtcSoft.VideoSurveillance.Blazor.App/  # 🌐 Blazor WebAssembly UI (MudBlazor)
│   └── AtcSoft.VideoSurveillance.Aspire/      # ☁️ Aspire AppHost (orchestration + dashboard)
│
├── setup/
│   ├── AtcSoft.CameraWall.Installer/          # 📦 WiX MSI installer (CameraWall)
│   └── AtcSoft.VideoSurveillance.Installer/   # 📦 WiX MSI installer (VideoSurveillance)
│
├── test/
│   ├── AtcSoft.VideoSurveillance.Core.Tests/      # ✅ xUnit v3 tests for Core
│   ├── AtcSoft.VideoSurveillance.Wpf.Core.Tests/  # ✅ xUnit v3 tests for Wpf.Core
│   ├── AtcSoft.VideoSurveillance.Api.Tests/        # ✅ xUnit v3 tests for API
│   └── AtcSoft.VideoEngine.Tests/                  # ✅ xUnit v3 tests for VideoEngine
│
├── docs/                        # 📚 System documentation
│   ├── architecture.md          # 🏛️ Architecture diagrams and assembly details
│   ├── disk-space-guard.md      # 💾 Disk space guard
│   ├── motion-detection.md      # 🏃 Motion detection setup guide
│   ├── settings.md              # ⚙️ Complete settings reference
│   └── usb-cameras.md           # 🎥 USB camera support
│
└── plans/                       # 🗺️ Roadmaps and plans (not system documentation)
    ├── roadmap.md               # 🗺️ Roadmap home: every area, its status and what is next
    └── roadmap-*.md             # 🗺️ One roadmap per area
```

### 🔗 Dependency Graph

Arrows point from a project to the projects it depends on (compile-time `ProjectReference`).

```mermaid
flowchart TD
    subgraph Foundation["📚 Foundation (cross-platform, net10.0)"]
        Core[AtcSoft.VideoSurveillance.Core]
        VE[AtcSoft.VideoEngine]
    end

    subgraph Server["☁️ Server (REST + events)"]
        Contracts[AtcSoft.VideoSurveillance.Api.Contracts]
        Domain[AtcSoft.VideoSurveillance.Api.Domain]
        Api[AtcSoft.VideoSurveillance.Api]
        Blazor[AtcSoft.VideoSurveillance.Blazor.App]
    end

    subgraph Windows["🪟 Windows-only (net10.0-windows)"]
        VEDx[AtcSoft.VideoEngine.DirectX]
        VPWpf[AtcSoft.VideoPlayer.Wpf]
        WpfCore[AtcSoft.VideoSurveillance.Wpf.Core]
        CamWall[AtcSoft.CameraWall.Wpf]
        VsWpf[AtcSoft.VideoSurveillance.Wpf]
    end

    subgraph Apps["🚀 Applications"]
        CamApp[AtcSoft.CameraWall.Wpf.App]
        VsApp[AtcSoft.VideoSurveillance.Wpf.App]
        Aspire[AtcSoft.VideoSurveillance.Aspire]
    end

    %% Server stack
    Contracts --> Core
    Domain --> Contracts
    Domain --> Core
    Api --> Contracts
    Api --> Domain
    Api --> Core
    Api --> VE

    %% Windows / WPF stack
    VEDx --> VE
    VPWpf --> VEDx
    WpfCore --> Core
    WpfCore --> VPWpf
    CamWall --> WpfCore
    VsWpf --> Core
    VsWpf --> WpfCore

    %% App composition
    CamApp --> CamWall
    VsApp --> VsWpf
    Aspire --> Api
    Aspire --> Blazor
    Aspire --> CamApp
    Aspire --> VsApp
```

## 🔧 Services

Services are split between Core (shared) and WPF (UI-specific), all auto-registered via `[Registration]` attributes.

### 📚 Core Services (AtcSoft.VideoSurveillance.Core)

| Service | Description |
|---------|-------------|
| `ICameraStorageService` | 💾 Persistence for cameras and layouts |
| `IApplicationSettingsService` | ⚙️ Application settings with per-camera override support |
| `IRecordingService` | 🔴 Manual and motion-triggered recording management |
| `IRecordingSegmentationService` | ✂️ Clock-aligned recording file segmentation |
| `IMotionDetectionService` | 🏃 Frame-based motion detection with multi-bounding box support |
| `ITimelapseService` | ⏱️ Interval-based timelapse snapshot capture |
| `IThumbnailGeneratorService` | 🖼️ Recording thumbnail generation |
| `IMediaCleanupService` | 🧹 Automatic cleanup of old recordings and snapshots |
| `IGitHubReleaseService` | 🔄 GitHub-based update checking |
| `IMediaPipeline` | 🎬 Abstraction for video stream operations (record, capture frame) |
| `IMediaPipelineFactory` | 🏭 Creates `IMediaPipeline` instances per camera |

### 🖥️ WPF Services (AtcSoft.CameraWall.Wpf)

| Service | Description |
|---------|-------------|
| `ICameraWallManager` | 🎛️ Main facade for all camera wall operations |
| `IDialogService` | 💬 Dialog abstraction for camera config, settings, input, confirmations |

### 🔀 Per-Camera Override System

Every application-level setting can be overridden per camera. Override models use nullable properties -- `null` means "use application default":

```csharp
// Get effective value: per-camera override wins over app default
var timeout = settingsService.GetEffectiveValue(
    camera,
    settingsService.Connection.ConnectionTimeoutSeconds,
    o => o?.Connection.ConnectionTimeoutSeconds);
```

Override categories: Connection, CameraDisplay, Performance, Recording, MotionDetection (with nested BoundingBox).

## 🚀 Getting Started

### 📋 Prerequisites

- Windows 10/11
- .NET 10 SDK (for building from source)
- FFmpeg 9 shared libraries (for building from source; the apps offer to install them, see below)

### 📥 Install

Download the latest MSI installer from [GitHub Releases](https://github.com/davidkallesen/AtcSoft.VideoSurveillance/releases). The installers include FFmpeg, so there is nothing else to install.

### 🎞️ Install FFmpeg (building from source)

The video engine loads FFmpeg in-process, so when you run from source the FFmpeg DLLs have to be on the machine.

**Supported version: FFmpeg 9 (9.0.2), *shared* build, Windows x64.** The bindings (`Flyleaf.FFmpeg.Bindings` 9.0.2) load exactly one major version of each library (`avutil-61.dll`, `avcodec-62.dll`, `avformat-62.dll`, …), so FFmpeg 8 and older are not used. A *shared* build has these DLLs in its `bin` folder; the *essentials* and static builds contain only `ffmpeg.exe`, which won't work.

**The easy way: let the app install it.** When CameraWall or the VideoSurveillance WPF app starts without FFmpeg 9, it shows an *FFmpeg Setup* dialog. It says which FFmpeg is needed and what it found, and offers to:

- **Install FFmpeg**: downloads the pinned 9.0.2 build (about 100 MB) from the [gyan.dev mirror on GitHub](https://github.com/GyanD/codexffmpeg/releases), checks its SHA-256, and puts it in `%ProgramData%\AtcSoft\ffmpeg\9.0.2\bin`, where every app and the server find it. An FFmpeg you installed yourself is never touched.
- **Use this folder**: takes a folder you name, when it holds FFmpeg 9. The folder is remembered for the whole machine (`%ProgramData%\AtcSoft\ffmpeg\folder.txt`).

The server has no dialog. Without FFmpeg 9 it logs what is needed and exits with code 1. Start it once with `--install-ffmpeg` to install it:

```bash
dotnet run --project src/AtcSoft.VideoSurveillance.Api -- --install-ffmpeg
```

**By hand:** download a shared FFmpeg 9 build ([gyan.dev](https://www.gyan.dev/ffmpeg/builds/) `ffmpeg-9.0.2-full_build-shared`, or [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds/releases) `…-win64-gpl-shared-9.0.zip`) and copy everything in its `bin` folder to one of the folders below.

The engine looks in these folders, in this order, and uses the first one whose libraries are all FFmpeg 9:

| # | Folder | Notes |
|---|---|---|
| 1 | The folder the app is configured with | |
| 2 | `ATCSOFT_FFMPEG_PATH` | An environment variable naming a folder |
| 3 | The folder chosen in the set-up dialog | `%ProgramData%\AtcSoft\ffmpeg\folder.txt` |
| 4 | `<app folder>\ffmpeg` | What the installers use |
| 5 | `%ProgramData%\AtcSoft\ffmpeg\9.0.2\bin` | What the set-up dialog and `--install-ffmpeg` install |
| 6 | `%ProgramFiles%\ffmpeg\bin`, `%LOCALAPPDATA%\ffmpeg\bin`, `C:\ffmpeg\bin` | |
| 7 | `<app folder>`, then each folder on `PATH` | |

A folder with another FFmpeg version is reported, not used. The server's live streams use `ffmpeg.exe` from the same folder.

This applies to the CameraWall app, the VideoSurveillance WPF app and the API server (also when started through Aspire). NuGet consumers of `AtcSoft.VideoEngine` deploy the DLLs with their app, or call `FFmpegInstaller.InstallAsync`.

### 🔨 Build from Source

```bash
dotnet build
dotnet run --project src/AtcSoft.CameraWall.Wpf.App
```

### ☁️ Run Server Edition with Aspire

```bash
dotnet run --project src/AtcSoft.VideoSurveillance.Aspire
```

This starts the Aspire dashboard and the REST API server. The dashboard provides monitoring, logs, and traces for all orchestrated services.

### 🌍 Run Server Edition Standalone

```bash
dotnet run --project src/AtcSoft.VideoSurveillance.Api
```

### 📁 User Data

Application data is stored in:
```
%ProgramData%/AtcSoft/CameraWall/
├── cameras.json        # 📷 Camera configurations and layouts
├── settings.json       # ⚙️ Application settings
├── logs/               # 📝 Debug log files (when enabled)
├── snapshots/          # 🖼️ Camera snapshots and timelapse frames
└── recordings/         # 🎥 Camera recordings with thumbnails
```

## 📖 Usage

1. **➕ Add Camera** - Click "Add Camera" in the Cameras tab to open the configuration dialog
2. **🔍 Network Scanner** - Use the built-in scanner in the Add Camera dialog to discover cameras
3. **📐 Layouts** - Use the Layouts tab to create, delete, or set startup layouts
4. **🖱️ Context Menu** - Right-click any camera tile for actions (Edit, Delete, Full Screen, Swap, Snapshot, Recording, Reconnect)
5. **✋ Drag-and-Drop** - Drag camera tiles to reorder them in the grid
6. **🔲 Full Screen** - Double-click a camera tile or use context menu for full screen view
7. **⚙️ Settings** - Click the Settings button in the View tab to configure all application settings
8. **🎥 Recordings** - Use the View tab to browse and playback recorded videos
9. **🔄 Check for Updates** - Use Help tab to check for new versions on GitHub

## 📄 License

MIT