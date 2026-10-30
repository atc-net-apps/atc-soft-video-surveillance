namespace AtcSoft.VideoSurveillance.Enums;

/// <summary>
/// Identifies the kind of lifecycle transition raised by
/// <see cref="Services.IUsbCameraLifecycleCoordinator.StateChanged"/>.
/// </summary>
public enum UsbCameraLifecyclePhase
{
    /// <summary>
    /// The physical USB device is no longer enumerable. The camera
    /// transitions to <see cref="ConnectionState.DeviceUnplugged"/>
    /// and reconnect-backoff is suspended.
    /// </summary>
    Unplugged = 0,

    /// <summary>
    /// A previously-unplugged USB device has reappeared. Backoff is
    /// cleared so the next connection-service tick attempts the
    /// camera fresh.
    /// </summary>
    Replugged = 1,
}