# AtcSoft.Video.Surveillance

Shared models, enums, events, service interfaces and helpers for the AtcSoft video surveillance libraries. No UI dependencies.

Contains `CameraConfiguration` — the model the camera controls bind to — along with the connection, display, stream and recording settings that hang off it.

Target framework: `net10.0`.

You normally get this package as a dependency of [`AtcSoft.Video.Surveillance.Wpf.Core`](https://www.nuget.org/packages/AtcSoft.Video.Surveillance.Wpf.Core), which provides the `CameraTile` and `CameraGrid` controls. Reference it directly if you need the models without any WPF dependency — for example in a service or an ASP.NET Core host.

## Links

- [Source and documentation](https://github.com/atc-net-apps/atc-soft-video-surveillance)
