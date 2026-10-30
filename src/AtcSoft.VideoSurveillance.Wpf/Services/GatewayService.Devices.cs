namespace AtcSoft.VideoSurveillance.Wpf.Services;

using ApiUsbDeviceDescriptor = global::VideoSurveillance.Generated.Devices.Models.UsbDeviceDescriptor;

/// <summary>
/// Gateway service - Devices operations using generated endpoints.
/// Server-side USB camera enumeration; the API client uses this
/// instead of probing the local host because the cameras attached to
/// the *server* are what the operator wants to manage.
/// </summary>
public sealed partial class GatewayService
{
    public async Task<ApiUsbDeviceDescriptor[]?> ListUsbDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await listUsbDevicesEndpoint
            .ExecuteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent.ToArray()
            : null;
    }
}