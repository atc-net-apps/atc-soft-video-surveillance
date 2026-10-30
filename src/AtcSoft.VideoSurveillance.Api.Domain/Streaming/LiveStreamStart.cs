namespace AtcSoft.VideoSurveillance.Api.Domain.Streaming;

/// <summary>
/// How a request to watch a camera went.
/// </summary>
/// <param name="Outcome">Started, the camera is not known, or the stream could not be started.</param>
/// <param name="LeaseId">The viewer's lease, when it started.</param>
/// <param name="PlaylistPath">The HLS playlist relative to the server (<c>/streams/...</c>), when it started.</param>
/// <param name="Reason">Why it did not start (already cleaned of camera credentials).</param>
public sealed record LiveStreamStart(
    LiveStreamOutcome Outcome,
    Guid LeaseId,
    string PlaylistPath,
    string Reason)
{
    public static LiveStreamStart Started(
        Guid leaseId,
        string playlistPath)
        => new(LiveStreamOutcome.Started, leaseId, playlistPath, string.Empty);

    public static LiveStreamStart CameraNotFound(Guid cameraId)
        => new(LiveStreamOutcome.CameraNotFound, Guid.Empty, string.Empty, $"Camera {cameraId} not found.");

    public static LiveStreamStart Failed(string reason)
        => new(LiveStreamOutcome.Failed, Guid.Empty, string.Empty, reason);
}