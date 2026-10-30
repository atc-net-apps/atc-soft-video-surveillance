# AtcSoft.VideoEngine

Cross-platform video engine built on in-process FFmpeg: demuxing, decoding, recording and frame capture for RTSP, HTTP and DirectShow (USB) sources.

Target framework: `net10.0`.

## Usage

Initialise the engine once at startup, then create players through the factory:

```csharp
VideoEngineBootstrap.Initialize(new VideoEngineConfig
{
    FFmpegPath = ffmpegFolder,          // folder holding the native FFmpeg DLLs
    PreferHardwareAcceleration = true,
});

var factory = new VideoPlayerFactory(loggerFactory);
IVideoPlayer player = factory.Create();
```

`VideoEngineConfig` also exposes `FFmpegLogLevel` and `DecoderThreads`. The parameterless `VideoEngineBootstrap.Initialize()` overload uses the defaults and probes for FFmpeg on the standard paths.

## FFmpeg is not included

This package contains no native binaries. Deploy an FFmpeg **9** shared build (`avutil-61.dll` and friends) with your application and point `FFmpegPath` at it, or install it at run time with `FFmpegInstaller.InstallAsync` (the pinned 9.0.2 build, checked against its SHA-256). `FFmpegRuntime.Check()` says whether the right FFmpeg is there and what was found; without it, `VideoEngineBootstrap.Initialize` throws `FFmpegNotReadyException`, whose `Check.Describe()` says what is needed.

## Related packages

- [`AtcSoft.VideoEngine.DirectX`](https://www.nuget.org/packages/AtcSoft.VideoEngine.DirectX) — D3D11VA hardware acceleration on Windows
- [`AtcSoft.VideoPlayer.Wpf`](https://www.nuget.org/packages/AtcSoft.VideoPlayer.Wpf) — WPF host control
- [`AtcSoft.VideoSurveillance.Wpf.Core`](https://www.nuget.org/packages/AtcSoft.VideoSurveillance.Wpf.Core) — ready-made camera tile and grid controls

## Links

- [Source and documentation](https://github.com/davidkallesen/AtcSoft.VideoSurveillance)
