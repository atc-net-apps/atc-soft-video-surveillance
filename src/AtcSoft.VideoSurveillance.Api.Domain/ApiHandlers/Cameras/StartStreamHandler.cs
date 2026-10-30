namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Cameras;

/// <summary>
/// Handler business logic for the StartStream operation: a viewer's lease on a
/// camera's live stream, answered when the stream can be played.
/// </summary>
public sealed class StartStreamHandler(ILiveStreamService streams) : IStartStreamHandler
{
    // A camera that could not be opened, or gave no picture in time, is
    // worth trying again shortly; it may be booting or reconnecting.
    private const int RetryAfterSeconds = 10;

    public async Task<StartStreamResult> ExecuteAsync(
        StartStreamParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var start = await streams
            .StartAsync(parameters.CameraId, cancellationToken)
            .ConfigureAwait(false);

        return start.Outcome switch
        {
            LiveStreamOutcome.Started => StartStreamResult.Ok(new StreamLease(
                LeaseId: start.LeaseId,
                CameraId: parameters.CameraId,
                PlaylistUrl: start.PlaylistPath,
                RenewSeconds: (int)streams.RenewInterval.TotalSeconds)),
            LiveStreamOutcome.CameraNotFound => StartStreamResult.NotFound(start.Reason),
            _ => StartStreamResult.ServiceUnavailable(start.Reason, retryAfter: RetryAfterSeconds),
        };
    }
}