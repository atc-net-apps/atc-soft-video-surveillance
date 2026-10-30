namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// A snapshot of a USB camera as reported by the host's enumerator
/// (Media Foundation on Windows, V4L2 on Linux, etc). Read-only by
/// design — descriptors are produced by
/// <see cref="Services.IUsbCameraEnumerator"/>, not edited.
/// </summary>
public sealed class UsbDeviceDescriptor
{
    public UsbDeviceDescriptor(
        string deviceId,
        string friendlyName,
        string? vendorId = null,
        string? productId = null,
        bool isPresent = true,
        IReadOnlyList<UsbStreamFormat>? capabilities = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentNullException.ThrowIfNull(friendlyName);

        DeviceId = deviceId;
        FriendlyName = friendlyName;
        VendorId = vendorId;
        ProductId = productId;
        IsPresent = isPresent;
        Capabilities = capabilities ?? [];
    }

    /// <summary>
    /// Stable device identifier (symbolic link). See
    /// <see cref="Models.Settings.UsbConnectionSettings.DeviceId"/>.
    /// </summary>
    public string DeviceId { get; }

    public string FriendlyName { get; }

    /// <summary>USB vendor id, hex 4-digit (e.g. <c>046d</c>).</summary>
    public string? VendorId { get; }

    /// <summary>USB product id, hex 4-digit (e.g. <c>085e</c>).</summary>
    public string? ProductId { get; }

    /// <summary>
    /// <see langword="true"/> when the enumerator reported this device
    /// in its most recent scan. Stored cameras can carry a descriptor
    /// with <see cref="IsPresent"/> = <see langword="false"/> so the UI
    /// can render a "device unplugged" state.
    /// </summary>
    public bool IsPresent { get; }

    /// <summary>
    /// Capture formats the device advertises. Consumers should treat
    /// this as authoritative — unsupported triples generally fail at
    /// stream open time.
    /// </summary>
    public IReadOnlyList<UsbStreamFormat> Capabilities { get; }

    /// <inheritdoc />
    public override string ToString()
        => $"UsbDeviceDescriptor {{ FriendlyName='{FriendlyName}', VID={VendorId ?? "?"}, PID={ProductId ?? "?"}, Present={IsPresent}, Caps={Capabilities.Count.ToString(CultureInfo.InvariantCulture)} }}";

    /// <summary>
    /// Identity equality — two descriptors are the same physical device
    /// iff their <see cref="DeviceId"/> matches.
    /// </summary>
    public bool IdentityEquals(UsbDeviceDescriptor? other)
        => other is not null &&
           string.Equals(DeviceId, other.DeviceId, StringComparison.OrdinalIgnoreCase);
}