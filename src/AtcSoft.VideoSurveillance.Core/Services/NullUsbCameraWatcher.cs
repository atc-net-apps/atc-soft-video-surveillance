namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// No-op watcher. DI fallback for hosts where hot-plug detection is
/// unavailable (or not wanted, such as the Aspire dashboard).
/// </summary>
[SuppressMessage("Major Code Smell", "S108:Add or remove the body of the method", Justification = "Intentional no-op fallback")]
public sealed class NullUsbCameraWatcher : IUsbCameraWatcher
{
    public event EventHandler<UsbCameraEventArgs>? DeviceArrived
    {
        add { /* no events ever raised */ }
        remove { /* no subscriptions to remove */ }
    }

    public event EventHandler<UsbCameraEventArgs>? DeviceRemoved
    {
        add { /* no events ever raised */ }
        remove { /* no subscriptions to remove */ }
    }

    public void Start()
    {
        // Null watcher: no underlying source to start.
    }

    public void Stop()
    {
        // Null watcher: nothing to stop.
    }

    public void Dispose()
    {
        // Null watcher: nothing to release.
    }
}