# Atc.Video.Player.Wpf

WPF `VideoHost` control that renders video from [`Atc.Video.Engine`](https://www.nuget.org/packages/Atc.Video.Engine) onto a DirectComposition surface, with a XAML overlay layer on top for your own content.

Target framework: `net10.0-windows`.

This is the low-level rendering control. If you want a ready-made camera tile with connection state, overlays and a grid layout, use [`AtcSoft.Video.Surveillance.Wpf.Core`](https://www.nuget.org/packages/AtcSoft.Video.Surveillance.Wpf.Core) instead — it builds on this package.

The DirectComposition surface is composed by the GPU rather than drawn through WPF's software pipeline, which is what keeps several simultaneous camera streams smooth.

## FFmpeg is not included

Playback requires the FFmpeg **8.x** shared native libraries to be deployed with your application; see the [`Atc.Video.Engine`](https://www.nuget.org/packages/Atc.Video.Engine) readme.

## Links

- [Source and documentation](https://github.com/atc-net-apps/atc-soft-video-surveillance)
