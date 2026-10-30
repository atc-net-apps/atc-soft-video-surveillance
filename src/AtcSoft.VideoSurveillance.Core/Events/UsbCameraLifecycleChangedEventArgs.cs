namespace AtcSoft.VideoSurveillance.Events;

/// <summary>
/// Fired by <see cref="Services.IUsbCameraLifecycleCoordinator"/> when
/// a stored USB camera transitions between <c>Unplugged</c> and
/// <c>Replugged</c>. Carries both the camera id (for routing into
/// <c>CameraConnectionService</c> and the clients' events) and the
/// device descriptor (for diagnostic logging).
/// </summary>
public sealed class UsbCameraLifecycleChangedEventArgs : EventArgs
{
    public UsbCameraLifecycleChangedEventArgs(
        Guid cameraId,
        UsbCameraLifecyclePhase phase,
        UsbDeviceDescriptor device)
    {
        ArgumentNullException.ThrowIfNull(device);
        CameraId = cameraId;
        Phase = phase;
        Device = device;
    }

    public Guid CameraId { get; }

    public UsbCameraLifecyclePhase Phase { get; }

    public UsbDeviceDescriptor Device { get; }
}