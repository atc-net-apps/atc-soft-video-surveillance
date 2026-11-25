namespace AtcSoft.Video.Surveillance.Api.Domain.ApiHandlers.Onvif;

/// <summary>
/// Handler business logic for the DiscoverOnvifCameras operation: the ONVIF devices that answer
/// a WS-Discovery probe on the server's networks.
/// </summary>
public sealed class DiscoverOnvifCamerasHandler(IOnvifDiscovery discovery) : IDiscoverOnvifCamerasHandler
{
    public async Task<DiscoverOnvifCamerasResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var found = await discovery.DiscoverAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        return DiscoverOnvifCamerasResult.Ok(new global::VideoSurveillance.Generated.Onvif.Models.OnvifDiscoveryResult(
            Devices: [.. found.Devices.Select(d => new OnvifDiscoveredDevice(d.Address, d.Port, d.Name, d.Hardware))],
            CouldProbe: found.CouldProbe));
    }
}