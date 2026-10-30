namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// The server's live streams, watched on a lease the viewer renews
/// (<see cref="GatewayService"/>; a seam for the tiles' tests).
/// </summary>
public interface IStreamLeaseGateway
{
    /// <summary>
    /// Takes a lease on a camera's stream; answered when it can be played.
    /// </summary>
    /// <exception cref="HttpRequestException">The server refused, with its reason, or could not be reached.</exception>
    Task<StreamLease> StartStreamAsync(
        Guid cameraId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renews a lease. Gone means the stream is to be started again.
    /// </summary>
    Task<StreamLeaseRenewal> RenewStreamLeaseAsync(
        Guid cameraId,
        Guid leaseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives a lease back; a server that is away lets it run out by itself.
    /// </summary>
    Task StopStreamAsync(
        Guid cameraId,
        Guid leaseId,
        CancellationToken cancellationToken = default);
}