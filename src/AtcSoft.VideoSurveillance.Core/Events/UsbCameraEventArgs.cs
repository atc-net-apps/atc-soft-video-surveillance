namespace AtcSoft.VideoSurveillance.Events;

/// <summary>
/// Carries a <see cref="UsbDeviceDescriptor"/> for the
/// <see cref="Services.IUsbCameraWatcher.DeviceArrived"/> /
/// <see cref="Services.IUsbCameraWatcher.DeviceRemoved"/> events.
/// </summary>
public sealed class UsbCameraEventArgs : EventArgs
{
    public UsbCameraEventArgs(UsbDeviceDescriptor device)
    {
        ArgumentNullException.ThrowIfNull(device);
        Device = device;
    }

    public UsbDeviceDescriptor Device { get; }
}