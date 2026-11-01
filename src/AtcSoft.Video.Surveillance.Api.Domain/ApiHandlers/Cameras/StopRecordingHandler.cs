namespace AtcSoft.Video.Surveillance.Api.Domain.ApiHandlers.Cameras;

/// <summary>
/// Handler business logic for the StopRecording operation. A camera the server records on
/// connect is recorded again at the next connection check; one someone started is not.
/// </summary>
public sealed class StopRecordingHandler(
    ICameraStorageService storage,
    IRecordingService recordingService,
    IRecordingIntents? intents = null) : IStopRecordingHandler
{
    public Task<StopRecordingResult> ExecuteAsync(
        StopRecordingParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var camera = storage.GetCameraById(parameters.CameraId);
        if (camera is null)
        {
            return Task.FromResult(StopRecordingResult.NotFound($"Camera {parameters.CameraId} not found."));
        }

        intents?.Drop(parameters.CameraId);
        recordingService.StopRecording(parameters.CameraId);

        var state = recordingService.GetRecordingState(parameters.CameraId);
        var status = new RecordingStatus(
            CameraId: parameters.CameraId,
            State: state.ToApiRecordingState(),
            FilePath: string.Empty,
            StartedAt: DateTimeOffset.UtcNow);

        return Task.FromResult(StopRecordingResult.Ok(status));
    }
}