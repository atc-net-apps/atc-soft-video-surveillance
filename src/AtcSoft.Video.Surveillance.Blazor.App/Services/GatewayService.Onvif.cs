namespace AtcSoft.Video.Surveillance.Blazor.App.Services;

/// <summary>
/// Gateway service - ONVIF: find the cameras on the server's networks, read a camera's streams.
/// </summary>
public sealed partial class GatewayService
{
    public async Task<GatewayResult<OnvifDiscoveryResult>> DiscoverOnvifCamerasAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await discoverOnvifCamerasEndpoint
            .ExecuteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? GatewayResult.Success(result.OkContent)
            : GatewayResult.Refused<OnvifDiscoveryResult>(result);
    }

    public async Task<GatewayResult<OnvifProfilesResult>> ReadOnvifProfilesAsync(
        OnvifProfilesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await readOnvifProfilesEndpoint
            .ExecuteAsync(new ReadOnvifProfilesParameters(Request: request), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? GatewayResult.Success(result.OkContent)
            : GatewayResult.Refused<OnvifProfilesResult>(result);
    }
}