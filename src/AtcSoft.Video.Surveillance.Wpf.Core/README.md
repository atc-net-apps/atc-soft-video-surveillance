# AtcSoft.Video.Surveillance.Wpf.Core

WPF controls for showing live camera streams: `CameraTile`, `CameraGrid` and `CameraOverlay`.

This is the entry point for embedding cameras in your own WPF application. The other AtcSoft.* packages come along as dependencies.

Target framework: `net10.0-windows10.0.19041.0`.

## Usage

Declare a tile in XAML:

```xml
<Window xmlns:cameras="clr-namespace:AtcSoft.Video.Surveillance.Wpf.Core.UserControls;assembly=AtcSoft.Video.Surveillance.Wpf.Core">
    <cameras:CameraTile x:Name="Tile" />
</Window>
```

Then give it a player factory and a camera:

```csharp
// Once at startup. Point FFmpegPath at the folder holding the native FFmpeg DLLs.
VideoEngineBootstrap.Initialize(new VideoEngineConfig { FFmpegPath = ffmpegFolder });

Tile.InitializeServices(
    recordingService: null,
    motionDetectionService: null,
    videoPlayerFactory: new VideoPlayerFactory(loggerFactory));

Tile.Camera = camera;   // AtcSoft.Video.Surveillance.Models.CameraConfiguration
```

`InitializeServices` must be called before the tile will play anything — without a player factory it stays `Disconnected`. Recording, motion detection, timelapse and toast notifications are all optional; pass `null` for the ones you don't want.

## FFmpeg is not included

This package does not ship the FFmpeg native libraries. Your application must deploy an FFmpeg **9** shared build (`avutil-61.dll` and friends) and point `VideoEngineConfig.FFmpegPath` at it. Or call `FFmpegSetup.EnsureReady()` before starting the engine: when FFmpeg 9 is not found it shows a dialog that installs it or takes a folder the user names, and returns the folder to start the engine with (`null` when the user chose to exit).

## Links

- [Source and documentation](https://github.com/atc-net-apps/atc-soft-video-surveillance)
