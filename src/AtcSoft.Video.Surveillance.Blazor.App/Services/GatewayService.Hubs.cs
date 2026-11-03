namespace AtcSoft.Video.Surveillance.Blazor.App.Services;

/// <summary>
/// Gateway service - camera hubs (motionEye, motionEyeOS): list a hub's cameras, add one.
/// </summary>
public sealed partial class GatewayService
{
    public async Task<GatewayResult<HubProbeResult>> ProbeHubAsync(
        HubProbeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await probeHubEndpoint
            .ExecuteAsync(new ProbeHubParameters(Request: request), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? GatewayResult.Success(result.OkContent)
            : GatewayResult.Refused<HubProbeResult>(result);
    }

    public async Task<GatewayResult<HubCameraAdded>> AddHubCameraAsync(
        AddHubCameraRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await addHubCameraEndpoint
            .ExecuteAsync(new AddHubCameraParameters(Request: request), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsCreated
            ? GatewayResult.Success(result.CreatedContent)
            : GatewayResult.Refused<HubCameraAdded>(result);
    }
}