namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Cameras;

/// <summary>
/// Handler business logic for the StopRecording operation.
/// </summary>
public sealed class StopRecordingHandler(
    ICameraStorageService storage,
    IRecordingService recordingService) : IStopRecordingHandler
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