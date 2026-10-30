namespace AtcSoft.Video.Surveillance.Wpf.Services;

using CoreUsbDeviceDescriptor = AtcSoft.Video.Surveillance.Models.UsbDeviceDescriptor;

/// <summary>
/// Thin abstraction over the <see cref="GatewayService"/> USB endpoint.
/// Exists so <see cref="RemoteUsbCameraEnumerator"/> can be tested
/// against a stub without standing up the concrete REST client.
/// </summary>
public interface IUsbCameraGateway
{
    /// <summary>
    /// Calls <c>GET /devices/usb</c> on the API server and maps each
    /// response item into a Core <see cref="CoreUsbDeviceDescriptor"/>.
    /// Returns <see langword="null"/> on transport failure or when the
    /// server reports 503 (USB enumeration not supported on that host).
    /// </summary>
    Task<IReadOnlyList<CoreUsbDeviceDescriptor>?> ListUsbDevicesAsync(
        CancellationToken cancellationToken = default);
}