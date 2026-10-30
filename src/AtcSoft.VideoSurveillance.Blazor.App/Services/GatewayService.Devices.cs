namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// Gateway service - Devices operations using generated endpoints.
/// </summary>
public sealed partial class GatewayService
{
    /// <summary>
    /// Gets the USB cameras the server can see.
    /// </summary>
    /// <returns>The cameras; null when the server cannot list them or did not answer.</returns>
    public async Task<UsbDeviceDescriptor[]?> GetUsbDevicesAsync(
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