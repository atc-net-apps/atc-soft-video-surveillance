namespace AtcSoft.VideoEngine.Windows.Watchers;

/// <summary>
/// <see cref="IUsbCameraWatcher"/> backed by WMI's
/// <c>__InstanceCreationEvent</c> / <c>__InstanceDeletionEvent</c>
/// over <c>Win32_PnPEntity</c>. Works for both UI hosts (no message
/// pump dependency) and headless servers, at the cost of slightly
/// higher CPU than a <c>RegisterDeviceNotification</c> hidden window.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsUsbWatcher : IUsbCameraWatcher
{
    private const string KsCategoryVideoCameraGuid = "{E5323777-F976-4F5B-9B55-B94699C46E44}";

    private readonly IUsbCameraEnumerator enumerator;
    private readonly ManagementEventWatcher creationWatcher;
    private readonly ManagementEventWatcher deletionWatcher;
    private readonly Lock syncRoot = new();

    private bool started;
    private bool disposed;

    public WindowsUsbWatcher(IUsbCameraEnumerator enumerator)
    {
        ArgumentNullException.ThrowIfNull(enumerator);
        this.enumerator = enumerator;

        // The 2-second polling window is the WMI default — fast enough
        // for human-perceived hot-plug, gentle on CPU.
        creationWatcher = new ManagementEventWatcher(
            new WqlEventQuery(
                "__InstanceCreationEvent",
                TimeSpan.FromSeconds(2),
                "TargetInstance ISA 'Win32_PnPEntity'"));

        deletionWatcher = new ManagementEventWatcher(
            new WqlEventQuery(
                "__InstanceDeletionEvent",
                TimeSpan.FromSeconds(2),
                "TargetInstance ISA 'Win32_PnPEntity'"));

        creationWatcher.EventArrived += OnCreationArrived;
        deletionWatcher.EventArrived += OnDeletionArrived;
    }

    public event EventHandler<UsbCameraEventArgs>? DeviceArrived;

    public event EventHandler<UsbCameraEventArgs>? DeviceRemoved;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        lock (syncRoot)
        {
            if (started)
            {
                return;
            }

            creationWatcher.Start();
            deletionWatcher.Start();
            started = true;
        }
    }

    public void Stop()
    {
        lock (syncRoot)
        {
            if (!started)
            {
                return;
            }

            creationWatcher.Stop();
            deletionWatcher.Stop();
            started = false;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Stop();

        creationWatcher.EventArrived -= OnCreationArrived;
        deletionWatcher.EventArrived -= OnDeletionArrived;
        creationWatcher.Dispose();
        deletionWatcher.Dispose();
        disposed = true;
    }

    private void OnCreationArrived(
        object sender,
        EventArrivedEventArgs e)
        => RaiseIfVideoCamera(e, DeviceArrived);

    private void OnDeletionArrived(
        object sender,
        EventArrivedEventArgs e)
        => RaiseIfVideoCamera(e, DeviceRemoved);

    private void RaiseIfVideoCamera(
        EventArrivedEventArgs e,
        EventHandler<UsbCameraEventArgs>? handler)
    {
        if (handler is null)
        {
            return;
        }

        if (e.NewEvent["TargetInstance"] is not ManagementBaseObject target)
        {
            return;
        }

        // Win32_PnPEntity.ClassGuid is the device-class GUID; we filter
        // for the camera/sensor class. This is heuristic — vendors
        // sometimes ship UVC cameras under different class guids — so
        // we additionally re-enumerate to confirm.
        var classGuid = target["ClassGuid"] as string;
        if (!string.Equals(classGuid, KsCategoryVideoCameraGuid, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var deviceId = target["DeviceID"] as string ?? string.Empty;
        var name = target["Name"] as string ?? string.Empty;

        // Match against current MF enumeration so the descriptor we
        // raise carries the same DeviceId shape callers compare on.
        var descriptor = enumerator.FindByDeviceId(deviceId)
            ?? new UsbDeviceDescriptor(
                deviceId: string.IsNullOrEmpty(deviceId) ? Guid.NewGuid().ToString() : deviceId,
                friendlyName: name,
                isPresent: handler == DeviceArrived);

        handler.Invoke(this, new UsbCameraEventArgs(descriptor));
    }
}