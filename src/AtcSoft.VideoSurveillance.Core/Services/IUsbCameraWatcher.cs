namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Raises <see cref="DeviceArrived"/> / <see cref="DeviceRemoved"/>
/// when USB cameras are plugged in or unplugged. Required so the
/// camera tile can transition cleanly to / from
/// <see cref="Enums.ConnectionState.DeviceUnplugged"/> rather than
/// thrashing in a reconnect loop.
/// </summary>
public interface IUsbCameraWatcher : IDisposable
{
    event EventHandler<UsbCameraEventArgs>? DeviceArrived;

    event EventHandler<UsbCameraEventArgs>? DeviceRemoved;

    /// <summary>
    /// Begins listening for hot-plug events. Idempotent.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops listening. Subscribers remain attached so a subsequent
    /// <see cref="Start"/> resumes notifications.
    /// </summary>
    void Stop();
}