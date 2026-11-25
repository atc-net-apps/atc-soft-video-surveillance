namespace AtcSoft.Video.Surveillance.Wpf.Services;

/// <summary>
/// The server's ONVIF operations, for the camera dialog (<see cref="GatewayService"/>; a seam for its tests).
/// </summary>
public interface IOnvifGateway
{
    /// <summary>
    /// Finds the ONVIF cameras on the server's networks.
    /// </summary>
    /// <exception cref="HttpRequestException">The server refused, with its reason, or could not be reached.</exception>
    Task<OnvifDiscoveryResult> DiscoverOnvifCamerasAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a camera's streams over ONVIF.
    /// </summary>
    /// <exception cref="HttpRequestException">The server refused, with its reason, or could not be reached.</exception>
    Task<OnvifProfilesResult> ReadOnvifProfilesAsync(
        OnvifProfilesRequest request,
        CancellationToken cancellationToken = default);
}