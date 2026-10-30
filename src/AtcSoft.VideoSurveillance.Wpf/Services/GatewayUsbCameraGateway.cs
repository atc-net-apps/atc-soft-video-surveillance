namespace AtcSoft.VideoSurveillance.Wpf.Services;

using ApiUsbDeviceDescriptor = global::VideoSurveillance.Generated.Devices.Models.UsbDeviceDescriptor;
using CoreUsbDeviceDescriptor = AtcSoft.VideoSurveillance.Models.UsbDeviceDescriptor;

/// <summary>
/// Concrete <see cref="IUsbCameraGateway"/> backed by the OpenAPI
/// generated <see cref="GatewayService"/> client. Maps the API DTO to
/// the Core <see cref="CoreUsbDeviceDescriptor"/> so consumers stay
/// behind the existing
/// <see cref="AtcSoft.VideoSurveillance.Services.IUsbCameraEnumerator"/>
/// contract.
/// </summary>
public sealed class GatewayUsbCameraGateway : IUsbCameraGateway
{
    private readonly GatewayService gatewayService;

    public GatewayUsbCameraGateway(GatewayService gatewayService)
    {
        ArgumentNullException.ThrowIfNull(gatewayService);
        this.gatewayService = gatewayService;
    }

    public async Task<IReadOnlyList<CoreUsbDeviceDescriptor>?> ListUsbDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        var apiDevices = await gatewayService
            .ListUsbDevicesAsync(cancellationToken)
            .ConfigureAwait(false);

        if (apiDevices is null)
        {
            return null;
        }

        var mapped = new List<CoreUsbDeviceDescriptor>(apiDevices.Length);
        foreach (var d in apiDevices)
        {
            mapped.Add(MapToCore(d));
        }

        return mapped;
    }

    private static CoreUsbDeviceDescriptor MapToCore(ApiUsbDeviceDescriptor api)
    {
        var caps = new List<AtcSoft.VideoSurveillance.Models.UsbStreamFormat>(api.Capabilities?.Count ?? 0);
        if (api.Capabilities is not null)
        {
            foreach (var c in api.Capabilities)
            {
                caps.Add(new AtcSoft.VideoSurveillance.Models.UsbStreamFormat
                {
                    Width = c.Width,
                    Height = c.Height,
                    FrameRate = c.FrameRate,
                    PixelFormat = c.PixelFormat ?? string.Empty,
                });
            }
        }

        return new CoreUsbDeviceDescriptor(
            deviceId: api.DeviceId,
            friendlyName: api.FriendlyName,
            vendorId: string.IsNullOrEmpty(api.VendorId) ? null : api.VendorId,
            productId: string.IsNullOrEmpty(api.ProductId) ? null : api.ProductId,
            isPresent: api.IsPresent,
            capabilities: caps);
    }
}