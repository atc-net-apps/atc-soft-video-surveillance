namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// No-op implementation. Registered as the DI default so the rest of
/// the stack composes on hosts that have no USB-camera support yet
/// (e.g. a Linux server before Phase 10 lands).
/// </summary>
public sealed class NullUsbCameraEnumerator : IUsbCameraEnumerator
{
    public static NullUsbCameraEnumerator Instance { get; } = new();

    public bool IsLocalEnumerator => false;

    public IReadOnlyList<UsbDeviceDescriptor> EnumerateDevices(
        CancellationToken cancellationToken = default)
        => [];

    public UsbDeviceDescriptor? FindByDeviceId(string deviceId) => null;

    public UsbDeviceDescriptor? FindByFriendlyName(string friendlyName) => null;
}