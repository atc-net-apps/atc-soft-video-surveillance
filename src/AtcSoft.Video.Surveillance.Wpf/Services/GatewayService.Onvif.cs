namespace AtcSoft.Video.Surveillance.Wpf.Services;

/// <summary>
/// Gateway service - ONVIF: find the cameras on the server's networks, read a camera's streams.
/// </summary>
public sealed partial class GatewayService
{
    public async Task<OnvifDiscoveryResult> DiscoverOnvifCamerasAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await discoverOnvifCamerasEndpoint
            .ExecuteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : throw Refused(result);
    }

    public async Task<OnvifProfilesResult> ReadOnvifProfilesAsync(
        OnvifProfilesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await readOnvifProfilesEndpoint
            .ExecuteAsync(new ReadOnvifProfilesParameters(Request: request), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : throw Refused(result);
    }
}