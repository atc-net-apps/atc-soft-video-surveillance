namespace AtcSoft.VideoSurveillance.Enums;

/// <summary>
/// Specifies the connection state of a camera stream.
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// The camera is disconnected.
    /// </summary>
    Disconnected,

    /// <summary>
    /// The camera is in the process of connecting.
    /// </summary>
    Connecting,

    /// <summary>
    /// The camera is connected and streaming.
    /// </summary>
    Connected,

    /// <summary>
    /// The camera is attempting to reconnect after a failure.
    /// </summary>
    Reconnecting,

    /// <summary>
    /// The camera connection has encountered an error.
    /// </summary>
    Error,

    /// <summary>
    /// USB-only state — the camera was previously enumerated but is no
    /// longer present (cable unplugged, device powered off). Distinct
    /// from <see cref="Error"/> because no amount of reconnect-backoff
    /// will help: the host must wait for the device to come back via
    /// <see cref="Services.IUsbCameraWatcher.DeviceArrived"/>.
    /// </summary>
    DeviceUnplugged,
}