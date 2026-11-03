namespace AtcSoft.Video.Surveillance.Wpf.Services;

/// <summary>
/// Gateway service - camera hubs (motionEye, motionEyeOS): list a hub's cameras, add one.
/// </summary>
public sealed partial class GatewayService
{
    public async Task<HubProbeResult> ProbeHubAsync(
        HubProbeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await probeHubEndpoint
            .ExecuteAsync(new ProbeHubParameters(Request: request), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : throw Refused(result);
    }

    public async Task<HubCameraAdded> AddHubCameraAsync(
        AddHubCameraRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await addHubCameraEndpoint
            .ExecuteAsync(new AddHubCameraParameters(Request: request), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsCreated
            ? result.CreatedContent
            : throw Refused(result);
    }
}