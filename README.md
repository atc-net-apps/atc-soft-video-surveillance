# 📹 AtcSoft Video Surveillance

[![Release](https://img.shields.io/github/v/release/atc-net-apps/atc-soft-video-surveillance?include_prereleases&logo=github&style=for-the-badge)](https://github.com/atc-net-apps/atc-soft-video-surveillance/releases)
[![Build](https://img.shields.io/github/actions/workflow/status/atc-net-apps/atc-soft-video-surveillance/ci.yml?branch=main&label=build&logo=github&style=for-the-badge)](https://github.com/atc-net-apps/atc-soft-video-surveillance/actions/workflows/ci.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&style=for-the-badge)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg?style=for-the-badge)](LICENSE)

Live view, recording, motion detection and an activity timeline for your RTSP, HTTP and USB cameras, on your own hardware. **No cloud, no subscription, no camera licences.**

> 📥 **Installing?** The **[installation guides](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki)** in the wiki take you through it step by step, for [CameraWall](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Install-CameraWall) and for the [Video Surveillance system](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Install-Video-Surveillance).

## 🧭 Two Ways to Run It

| | 🖥️ **CameraWall** | 🌍 **Video Surveillance** (server + clients) |
|---|---|---|
| **What it is** | One Windows app that connects to the cameras itself | A server that records around the clock, and apps that connect to it |
| **Good for** | One PC watching and recording a handful of cameras | A home or small business: record on one machine, watch from many |
| **Watch from** | That PC | The Windows desktop client, or a browser (web client) |
| **Users and roles** | The PC's own login | Users with roles, API keys, an audit log |
| **Integrations** | — | REST API (OpenAPI), live events, webhooks |
| **Runs on** | Windows | Windows today; Docker, Linux and NAS are [coming](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Platform-Support) |

## ✨ Key Features

| Area | Feature | Description |
|---|---|---|
| **Live view** | 🖥️ **Camera grid** | Auto-sized grid or fixed 1×1 to 4×4 with pages; named layouts with a start-up layout; drag to reorder |
| | 🔲 **Full screen and zoom** | Double-click for full screen; Ctrl+scroll or draw a rectangle to zoom, drag to pan; GPU-accelerated |
| | 🔄 **Rotation and overlay** | 0°/90°/180°/270° per camera, carried into motion boxes, snapshots and recordings; title, time and state on each tile |
| **Cameras** | 📷 **Any stream** | RTSP, HTTP and HTTPS cameras; USB webcams on Windows, with hot-plug; a network scanner and *Test Connection* in the camera dialog |
| | 🎛️ **Per-camera overrides** | Every setting (connection, display, recording, motion) can differ per camera |
| **Recording** | 🔴 **When you want** | By hand, on connect, or on motion with a post-motion time and a cooldown; MKV or MP4, copied as the camera sends it or turned into H.264 |
| | ✂️ **Segments and thumbnails** | Clock-aligned files (every hour, or every 15 minutes at :00, :15, …), a thumbnail and preview pictures per recording |
| | ♾️ **Never interrupted** | Recordings go on while you browse recordings, open a camera in full screen or switch layouts |
| **Motion and timeline** | 🏃 **Motion detection with tracking** | Moving objects tracked across frames and boxed live; ignore and watch zones per camera; wind and noise filtered out |
| | 🕒 **Activity timeline** | Every camera's activity on one timeline, with the reason for each gap (offline, not recording, disk full); hover for a picture, *play events only*, *play all from here* |
| | 🔎 **Analyse and review** | *Analyse recording* builds the timeline for older recordings; mark a detection right or wrong to tune the detector |
| **Storage** | 💾 **Disk space guard** | Keeps a minimum free on every disk that holds media, deleting the oldest recordings first |
| | 🧹 **Retention** | Removes recordings and snapshots older than you choose; optional folder per day |
| **Server** | 🌐 **REST API** | OpenAPI 3.2 contract, ProblemDetails errors, rate limits; Scalar in development |
| | 🌊 **Live events** | Connection, recording, motion and USB events as Server-Sent Events, resumable after a dropped connection |
| | 📺 **Live streams** | HLS streams per camera with leases, so a stream stops when its last viewer leaves |
| | 🪝 **Webhooks** | Signed HTTP calls on the events you pick |
| | 🔐 **Users and security** | Users with roles (viewer, operator, admin), API keys, an audit log; camera passwords and webhook secrets encrypted at rest |
| **Clients** | 🖥️ **Desktop and web** | A Windows desktop client and a Blazor web client, with the same views |
| | 🌍 **Four languages** | English (US and UK), Danish and German; dark and light themes |

## 📥 Install

| You want | Download from the [latest release](https://github.com/atc-net-apps/atc-soft-video-surveillance/releases/latest) | Guide |
|---|---|---|
| CameraWall | `AtcSoft.Video.Surveillance.CameraWall.Installer.msi` | [Install CameraWall](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Install-CameraWall) |
| The desktop client of the Video Surveillance system | `AtcSoft.Video.Surveillance.Client.Installer.msi` | [Install Video Surveillance](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Install-Video-Surveillance) |
| The server | No installer yet: built from source today | [The server on Windows today](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Install-Video-Surveillance#-the-server-on-windows-today) |

## 📋 Requirements

- **Windows 10** (version 2004 or later) or **Windows 11**, 64-bit
- The **[.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0)** for the installers (the server needs the ASP.NET Core Runtime 10)
- Cameras that stream **RTSP**, **HTTP** or **HTTPS**, or **USB** webcams
- **FFmpeg 9** is included in the installers; see [FFmpeg](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Install-FFmpeg) when building from source

What runs where, and what is coming (a server installer, Docker, Linux, UGREEN and Synology NAS): **[Platform support](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Platform-Support)**.

## 🏗️ Architecture

| Layer | Projects | Role |
|---|---|---|
| **Core** | `AtcSoft.Video.Surveillance` | Models, settings, motion detection, recording paths, disk space guard; no UI |
| **Video engine** | `Atc.Video.Engine`, `Atc.Video.Engine.DirectX`, `Atc.Video.Player.Wpf` | Demux, decode, record and capture with in-process FFmpeg; D3D11VA rendering in WPF |
| **Desktop** | `…Wpf.Core`, `…CameraWall.Wpf(.App)`, `…Wpf(.App)`, `…Windows` | CameraWall and the desktop client (WPF, Fluent.Ribbon); USB and thumbnails on Windows |
| **Server** | `…Api`, `…Api.Domain`, `…Api.Contracts` | ASP.NET Core host; handlers; contracts generated from [`VideoSurveillance.yaml`](src/VideoSurveillance.yaml) by [Atc.Rest.Api.SourceGenerator](https://github.com/atc-net/atc-rest-api-source-generator) |
| **Web** | `…Blazor.App` | Blazor WebAssembly client (MudBlazor) |
| **Dev** | `…Aspire` | Aspire AppHost: server, web and desktop clients with a dashboard |

The full picture, with diagrams: **[Architecture](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Architecture)**.

## ✅ Quality

- **About 2,600 tests**: unit tests per project, and host tests that run the real server in-process against a temporary data folder and a live FFmpeg test stream
- **End-to-end smoke tests** start CameraWall, the server, the desktop client and the web client, open every window, page and dialog (FlaUI and Playwright), and fail on a binding error or an unhandled exception; one records three cameras while the user browses recordings and switches layouts ([E2E smoke tests](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/E2E-Smoke-Tests))
- **A motion detection storybook**: scenes with known content, scored against a baseline that a change may not make worse ([storybook](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Motion-Detection-Storybook))
- **Benchmarks** for the archive walks and the activity caches (`benchmark/`)
- Strict analyzers (StyleCop, Meziantou, Sonar, AsyncFixer), warnings as errors

## 🔨 Build from Source

```bash
dotnet build
dotnet run --project src/AtcSoft.Video.Surveillance.CameraWall.Wpf.App   # CameraWall
dotnet run --project src/AtcSoft.Video.Surveillance.Aspire                # server, web and desktop clients
dotnet test
```

Needs the .NET 10 SDK and FFmpeg 9 (the apps offer to install it). More in **[Building from source](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Building-from-Source)**.

## 📚 WIKI - Documentation

Read the full documentation on the **[WIKI](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki)**.

| Document | Description |
|---|---|
| [🖥️ Install CameraWall](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Install-CameraWall) | Install, first start, where data is kept, updating |
| [🌍 Install Video Surveillance](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Install-Video-Surveillance) | The server, the desktop client and the web client, per platform |
| [🧭 Platform support](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Platform-Support) | What runs on Windows, Linux, macOS and NAS today, and what is coming |
| [🎞️ FFmpeg](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Install-FFmpeg) | The FFmpeg 9 the apps need, and where they look for it |
| [⚙️ Settings reference](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Settings-Reference) | Every setting, application-wide and per camera |
| [🏃 Motion detection](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Motion-Detection) | How detection works, its settings and motion recording |
| [🎥 USB cameras](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/USB-Cameras) | Webcams in CameraWall and on the server |
| [📡 Camera hubs](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Camera-Hubs) | Cameras of a motionEye or motionEyeOS (a Raspberry Pi with USB cameras) in CameraWall |
| [💾 Disk space guard](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Disk-Space-Guard) | Keeping disks from filling up |
| [🩺 Diagnostics](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Diagnostics) | What CameraWall records for a long test run, and how to read it |
| [🖧 Server reference](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Server-Reference) | Configuration, sign-in and roles, events, live streams, webhooks |
| [🔨 Building from source](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Building-from-Source) | For developers |
| [🏛️ Architecture](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Architecture) | Assemblies, layers and data flow |
| [🧪 E2E smoke tests](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/E2E-Smoke-Tests) | The end-to-end walks of every app |
| [🎬 Motion detection storybook](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Motion-Detection-Storybook) | Measuring the detector on scenes with known content |
| [🧪 24/7 soak test](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Soak-Test) | The procedure for a long stability run |

## 🗺️ Roadmap

What is coming, and in which order, is in **[platform support](https://github.com/atc-net-apps/atc-soft-video-surveillance/wiki/Platform-Support#coming)** in the wiki. Found a problem or missing something? [Open an issue](https://github.com/atc-net-apps/atc-soft-video-surveillance/issues).

## 📄 License

[MIT](LICENSE)