namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Lists USB cameras visible to the host. Implementations are
/// platform-specific (Media Foundation on Windows, V4L2 on Linux,
/// AVFoundation on macOS); the API client uses a remote
/// implementation that delegates over HTTP.
/// </summary>
public interface IUsbCameraEnumerator
{
    /// <summary>
    /// <see langword="true"/> when the enumerator runs on the same
    /// machine as the WPF process (Media Foundation, V4L2, AVFoundation).
    /// <see langword="false"/> when the enumerator delegates to a remote
    /// host over the wire (the API client's gateway-backed enumerator)
    /// or when no enumerator is wired up. Drives whether the camera
    /// configuration dialog renders the in-process
    /// <c>LabelUsbCameraPicker</c> (which depends on local WinRT
    /// hardware APIs) or the remote dropdown.
    /// </summary>
    bool IsLocalEnumerator { get; }

    /// <summary>
    /// Returns the set of currently-present USB cameras. Order is not
    /// guaranteed; callers should sort by friendly name themselves.
    /// </summary>
    IReadOnlyList<UsbDeviceDescriptor> EnumerateDevices(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Locates a device by its stable
    /// <see cref="UsbDeviceDescriptor.DeviceId"/>. Used during
    /// connect-on-startup to verify that a stored camera is still
    /// present. Returns <see langword="null"/> when the device is no
    /// longer enumerable.
    /// </summary>
    UsbDeviceDescriptor? FindByDeviceId(string deviceId);

    /// <summary>
    /// Friendly-name fallback for legacy stored cameras that pre-date
    /// the symbolic-link identifier. Prefer
    /// <see cref="FindByDeviceId"/>.
    /// </summary>
    UsbDeviceDescriptor? FindByFriendlyName(string friendlyName);
}