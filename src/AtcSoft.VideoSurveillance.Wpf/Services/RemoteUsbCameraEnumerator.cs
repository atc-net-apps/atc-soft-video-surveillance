namespace AtcSoft.VideoSurveillance.Wpf.Services;

using IUsbCameraEnumerator = AtcSoft.VideoSurveillance.Services.IUsbCameraEnumerator;
using UsbDeviceDescriptor = AtcSoft.VideoSurveillance.Models.UsbDeviceDescriptor;

/// <summary>
/// API-client implementation of <see cref="IUsbCameraEnumerator"/>.
/// Lists the USB cameras attached to the *server* host (not the client
/// PC) by calling <c>GET /devices/usb</c>. Caches the most recent
/// response for a short TTL so repeated dropdown renders don't fan out
/// to the server.
/// </summary>
/// <remarks>
/// The <see cref="IUsbCameraEnumerator"/> contract is intentionally
/// synchronous — it's invoked from WPF binding contexts where
/// returning a Task would force every consumer through the dispatcher.
/// We bridge to the async gateway via <c>GetAwaiter().GetResult()</c>
/// inside the cache TTL window; with the 5-second cache this collapses
/// to one HTTP call per refresh interval rather than one per binding
/// query.
/// </remarks>
public sealed class RemoteUsbCameraEnumerator : IUsbCameraEnumerator
{
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromSeconds(5);

    private readonly IUsbCameraGateway gateway;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan cacheTtl;
    private readonly Lock syncRoot = new();

    private IReadOnlyList<UsbDeviceDescriptor> cached = [];
    private DateTimeOffset cachedAtUtc = DateTimeOffset.MinValue;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="RemoteUsbCameraEnumerator"/> class with the system
    /// clock and the default 5-second cache TTL.
    /// </summary>
    public RemoteUsbCameraEnumerator(IUsbCameraGateway gateway)
        : this(gateway, TimeProvider.System, DefaultCacheTtl)
    {
    }

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="RemoteUsbCameraEnumerator"/> class with custom time
    /// and TTL — used by tests to advance time deterministically.
    /// </summary>
    internal RemoteUsbCameraEnumerator(
        IUsbCameraGateway gateway,
        TimeProvider timeProvider,
        TimeSpan cacheTtl)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.gateway = gateway;
        this.timeProvider = timeProvider;
        this.cacheTtl = cacheTtl;
    }

    public bool IsLocalEnumerator => false;

    public IReadOnlyList<UsbDeviceDescriptor> EnumerateDevices(
        CancellationToken cancellationToken = default)
    {
        lock (syncRoot)
        {
            var now = timeProvider.GetUtcNow();
            if (now - cachedAtUtc < cacheTtl && cachedAtUtc != DateTimeOffset.MinValue)
            {
                return cached;
            }

            // Sync-over-async — see class remarks. Cancellation flows
            // through the wrapped Task so a torn-down dialog can still
            // abort an in-flight HTTP call promptly.
            var fetched = gateway
                .ListUsbDevicesAsync(cancellationToken)
                .GetAwaiter()
                .GetResult();

            // 503 / null response means "server doesn't support USB
            // enumeration" (e.g. Linux server before V4L2 ships). Keep
            // the previous cached list rather than wiping — better
            // user-perceived continuity.
            if (fetched is null)
            {
                return cached;
            }

            cached = fetched;
            cachedAtUtc = now;
            return cached;
        }
    }

    public UsbDeviceDescriptor? FindByDeviceId(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return null;
        }

        foreach (var device in EnumerateDevices())
        {
            if (string.Equals(device.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                return device;
            }
        }

        return null;
    }

    public UsbDeviceDescriptor? FindByFriendlyName(string friendlyName)
    {
        if (string.IsNullOrEmpty(friendlyName))
        {
            return null;
        }

        foreach (var device in EnumerateDevices())
        {
            if (string.Equals(device.FriendlyName, friendlyName, StringComparison.OrdinalIgnoreCase))
            {
                return device;
            }
        }

        return null;
    }
}