namespace AtcSoft.VideoSurveillance.Api.Domain.Streaming;

/// <summary>
/// The live streams viewers watch, each on a lease it renews: a lease that is
/// not renewed ends by itself, and a stream stops with its last lease.
/// </summary>
public interface ILiveStreamService
{
    /// <summary>
    /// Gets how often a viewer renews its lease.
    /// </summary>
    TimeSpan RenewInterval { get; }

    /// <summary>
    /// Takes a lease on a camera's stream, and answers when it can be played.
    /// </summary>
    Task<LiveStreamStart> StartAsync(
        Guid cameraId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Renews a lease; <see langword="false"/> when there is no such lease on the camera.
    /// </summary>
    bool Renew(
        Guid cameraId,
        Guid leaseId);

    /// <summary>
    /// Gives a lease back; one that is already gone is no error.
    /// </summary>
    void Release(
        Guid cameraId,
        Guid leaseId);
}