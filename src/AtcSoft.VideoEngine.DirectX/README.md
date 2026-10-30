# AtcSoft.VideoEngine.DirectX

Windows GPU acceleration for [`AtcSoft.VideoEngine`](https://www.nuget.org/packages/AtcSoft.VideoEngine): D3D11VA hardware decoding, the DirectX Video Processor for colour conversion and scaling, and swap-chain presentation.

Target framework: `net10.0-windows`.

You normally get this package as a dependency of [`AtcSoft.VideoPlayer.Wpf`](https://www.nuget.org/packages/AtcSoft.VideoPlayer.Wpf) rather than referencing it directly. Add it explicitly only if you are building your own renderer on top of the engine.

Hardware acceleration is requested through `VideoEngineConfig.PreferHardwareAcceleration`, and the engine falls back to CPU decoding when the GPU path is unavailable.

## Links

- [Source and documentation](https://github.com/davidkallesen/AtcSoft.VideoSurveillance)
