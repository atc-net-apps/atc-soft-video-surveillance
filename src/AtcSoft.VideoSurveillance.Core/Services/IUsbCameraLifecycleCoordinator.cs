namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Owns the <see cref="IUsbCameraWatcher"/> subscription on behalf of
/// the connection-service layer and maintains the set of cameras whose
/// underlying USB device is currently unplugged. Consumers query
/// <see cref="IsUnplugged"/> to short-circuit reconnect attempts and
/// subscribe to <see cref="StateChanged"/> to react to transitions.
/// </summary>
public interface IUsbCameraLifecycleCoordinator : IDisposable
{
    /// <summary>
    /// Raised when a stored USB camera transitions between
    /// <see cref="UsbCameraLifecyclePhase.Unplugged"/> and
    /// <see cref="UsbCameraLifecyclePhase.Replugged"/>. Network cameras
    /// (and unknown devices that don't match any stored camera) never
    /// trigger this event.
    /// </summary>
    event EventHandler<UsbCameraLifecycleChangedEventArgs>? StateChanged;

    /// <summary>
    /// <see langword="true"/> when the camera was last seen unplugged
    /// and has not yet been re-enumerated. Connection services should
    /// skip cameras in this state — there is nothing to reconnect to.
    /// </summary>
    bool IsUnplugged(Guid cameraId);

    /// <summary>
    /// Begins listening for hot-plug events on the underlying watcher.
    /// Idempotent — safe to call multiple times.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops listening. Subscribers remain attached so a subsequent
    /// <see cref="Start"/> resumes notifications.
    /// </summary>
    void Stop();

    /// <summary>
    /// Drops any unplugged-state record for the given camera id. Called
    /// after a camera is deleted from storage so a stale entry doesn't
    /// linger in the coordinator's set — without this, every USB camera
    /// deleted while unplugged would leak an entry until process restart.
    /// No-op when the camera was never marked unplugged.
    /// </summary>
    void ClearUnpluggedState(Guid cameraId);
}