namespace AtcSoft.VideoEngine.Windows.MediaFoundation;

/// <summary>
/// <see cref="IUsbCameraEnumerator"/> backed by Media Foundation's
/// <c>MFEnumDeviceSources</c>. Capability discovery (resolution × FPS
/// × pixfmt) is left empty in this first cut — populated lazily by
/// the dialog when the user picks a device.
/// </summary>
public sealed class MediaFoundationEnumerator : IUsbCameraEnumerator
{
    private readonly Interop.IMfDeviceProbe probe;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="MediaFoundationEnumerator"/> class with the
    /// real Media Foundation probe.
    /// </summary>
    public MediaFoundationEnumerator()
        : this(new Interop.MediaFoundationDeviceProbe())
    {
    }

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="MediaFoundationEnumerator"/> class with a custom
    /// probe — used by tests to inject a fake.
    /// </summary>
    internal MediaFoundationEnumerator(Interop.IMfDeviceProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        this.probe = probe;
    }

    /// <inheritdoc />
    public bool IsLocalEnumerator => true;

    /// <inheritdoc />
    public IReadOnlyList<UsbDeviceDescriptor> EnumerateDevices(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var rows = probe.EnumerateVideoCaptureDevices();
        var descriptors = new List<UsbDeviceDescriptor>(rows.Count);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (vid, pid) = UsbSymbolicLinkParser.Parse(row.SymbolicLink);

            var capabilities = new List<UsbStreamFormat>(row.Capabilities.Count);
            foreach (var c in row.Capabilities)
            {
                capabilities.Add(new UsbStreamFormat
                {
                    Width = c.Width,
                    Height = c.Height,
                    FrameRate = c.FrameRate,
                    PixelFormat = c.PixelFormat,
                });
            }

            descriptors.Add(new UsbDeviceDescriptor(
                deviceId: row.SymbolicLink,
                friendlyName: row.FriendlyName,
                vendorId: vid,
                productId: pid,
                isPresent: true,
                capabilities: capabilities));
        }

        // Sort by friendly name for stable UI ordering across enumerations.
        descriptors.Sort((a, b) =>
            string.Compare(a.FriendlyName, b.FriendlyName, StringComparison.OrdinalIgnoreCase));

        return descriptors;
    }

    /// <inheritdoc />
    public UsbDeviceDescriptor? FindByDeviceId(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return null;
        }

        foreach (var descriptor in EnumerateDevices())
        {
            if (descriptor.IdentityEquals(new UsbDeviceDescriptor(deviceId, string.Empty)))
            {
                return descriptor;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public UsbDeviceDescriptor? FindByFriendlyName(string friendlyName)
    {
        if (string.IsNullOrEmpty(friendlyName))
        {
            return null;
        }

        foreach (var descriptor in EnumerateDevices())
        {
            if (string.Equals(descriptor.FriendlyName, friendlyName, StringComparison.OrdinalIgnoreCase))
            {
                return descriptor;
            }
        }

        return null;
    }
}