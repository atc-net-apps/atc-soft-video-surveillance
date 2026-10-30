namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Default <see cref="IUsbCameraLifecycleCoordinator"/>. Subscribes to
/// the host's <see cref="IUsbCameraWatcher"/> and translates raw
/// device-arrival/removal events into per-camera lifecycle transitions
/// keyed by <see cref="Guid"/>. Resolution from device-id to camera-id
/// goes through <see cref="ICameraStorageService"/> so the coordinator
/// stays in sync with stored data without holding its own state copy.
/// </summary>
public sealed class UsbCameraLifecycleCoordinator : IUsbCameraLifecycleCoordinator
{
    private readonly IUsbCameraWatcher watcher;
    private readonly ICameraStorageService storage;
    private readonly ConcurrentDictionary<Guid, byte> unpluggedCameras = new();
    private readonly Lock syncRoot = new();

    private bool subscribed;
    private bool disposed;

    public UsbCameraLifecycleCoordinator(
        IUsbCameraWatcher watcher,
        ICameraStorageService storage)
    {
        ArgumentNullException.ThrowIfNull(watcher);
        ArgumentNullException.ThrowIfNull(storage);
        this.watcher = watcher;
        this.storage = storage;
    }

    public event EventHandler<UsbCameraLifecycleChangedEventArgs>? StateChanged;

    public bool IsUnplugged(Guid cameraId)
        => unpluggedCameras.ContainsKey(cameraId);

    public void ClearUnpluggedState(Guid cameraId)
        => unpluggedCameras.TryRemove(cameraId, out _);

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        lock (syncRoot)
        {
            if (subscribed)
            {
                return;
            }

            watcher.DeviceArrived += OnDeviceArrived;
            watcher.DeviceRemoved += OnDeviceRemoved;
            watcher.Start();
            subscribed = true;
        }
    }

    public void Stop()
    {
        lock (syncRoot)
        {
            if (!subscribed)
            {
                return;
            }

            watcher.DeviceArrived -= OnDeviceArrived;
            watcher.DeviceRemoved -= OnDeviceRemoved;
            watcher.Stop();
            subscribed = false;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Stop();
        disposed = true;
    }

    private void OnDeviceArrived(
        object? sender,
        UsbCameraEventArgs e)
    {
        var cameraId = ResolveCameraId(e.Device.DeviceId);
        if (cameraId is null)
        {
            return;
        }

        // Only fire Replugged when the camera was previously marked
        // unplugged — a fresh DeviceArrived from the watcher's first
        // poll otherwise looks identical to a "new device plugged in
        // for the first time" event we don't want to surface.
        if (!unpluggedCameras.TryRemove(cameraId.Value, out _))
        {
            return;
        }

        StateChanged?.Invoke(
            this,
            new UsbCameraLifecycleChangedEventArgs(
                cameraId.Value,
                UsbCameraLifecyclePhase.Replugged,
                e.Device));
    }

    private void OnDeviceRemoved(
        object? sender,
        UsbCameraEventArgs e)
    {
        var cameraId = ResolveCameraId(e.Device.DeviceId);
        if (cameraId is null)
        {
            return;
        }

        // Only fire on the first transition to Unplugged — duplicate
        // removal events (the WMI watcher can occasionally double-fire
        // on driver reset) shouldn't generate a stream of identical
        // client notifications.
        if (!unpluggedCameras.TryAdd(cameraId.Value, 0))
        {
            return;
        }

        StateChanged?.Invoke(
            this,
            new UsbCameraLifecycleChangedEventArgs(
                cameraId.Value,
                UsbCameraLifecyclePhase.Unplugged,
                e.Device));
    }

    private Guid? ResolveCameraId(string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return null;
        }

        foreach (var camera in storage.GetAllCameras())
        {
            if (camera.Connection.Source != Enums.CameraSource.Usb)
            {
                continue;
            }

            var usb = camera.Connection.Usb;
            if (usb is null || string.IsNullOrEmpty(usb.DeviceId))
            {
                continue;
            }

            if (string.Equals(usb.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                return camera.Id;
            }
        }

        return null;
    }
}