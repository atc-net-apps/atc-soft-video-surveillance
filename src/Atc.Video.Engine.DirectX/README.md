# Atc.Video.Engine.DirectX

Windows GPU acceleration for [`Atc.Video.Engine`](https://www.nuget.org/packages/Atc.Video.Engine): D3D11VA hardware decoding, the DirectX Video Processor for colour conversion and scaling, and swap-chain presentation.

Target framework: `net10.0-windows`.

You normally get this package as a dependency of [`Atc.Video.Player.Wpf`](https://www.nuget.org/packages/Atc.Video.Player.Wpf) rather than referencing it directly. Add it explicitly only if you are building your own renderer on top of the engine.

Hardware acceleration is requested through `VideoEngineConfig.PreferHardwareAcceleration`, and the engine falls back to CPU decoding when the GPU path is unavailable.

## Links

- [Source and documentation](https://github.com/atc-net-apps/atc-soft-video-surveillance)
