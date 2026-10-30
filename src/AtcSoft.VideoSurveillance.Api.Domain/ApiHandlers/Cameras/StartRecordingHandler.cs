namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Cameras;

/// <summary>
/// Handler business logic for the StartRecording operation.
/// Requires an active IMediaPipeline and IRecordingService.
/// </summary>
public sealed class StartRecordingHandler(
    ICameraStorageService storage,
    IRecordingService recordingService,
    IMediaPipelineFactory pipelineFactory,
    ICameraPipelineHost? pipelineHost = null) : IStartRecordingHandler
{
    // Wait at most this long for the camera to reach the Connected state
    // before reporting failure. Longer than the typical RTSP open (1-3 s)
    // but short enough that a stuck client gets a definite answer.
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(15);

    public async Task<StartRecordingResult> ExecuteAsync(
        StartRecordingParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var camera = storage.GetCameraById(parameters.CameraId);
        if (camera is null)
        {
            return StartRecordingResult.NotFound($"Camera {parameters.CameraId} not found.");
        }

        if (recordingService.IsRecording(parameters.CameraId))
        {
            return StartRecordingResult.Conflict($"Camera {parameters.CameraId} is already recording.");
        }

        // Windows gives a USB camera to one program at a time, so it is
        // not opened here: it is recorded on the one pipeline it has, which
        // someone may be watching it on already.
        if (pipelineHost?.Hosts(camera) == true)
        {
            var outcome = await HostedCameraRecording
                .StartAsync(pipelineHost, recordingService, camera, ConnectionTimeout, cancellationToken)
                .ConfigureAwait(false);

            return outcome == HostedCameraRecordingOutcome.Started
                ? StartRecordingResult.Ok(BuildStatus(parameters.CameraId))
                : StartRecordingResult.Conflict(HostedCameraRecording.Describe(outcome, ConnectionTimeout));
        }

        var pipeline = pipelineFactory.Create(camera);
        try
        {
            // pipelineFactory.Create returns immediately; the actual RTSP
            // open happens on the engine thread. Wait for Connected (or a
            // hard failure) before reporting success so the client sees a
            // definite outcome instead of a fire-and-forget "ok".
            var connected = await PipelineConnectionWaiter
                .WaitForConnectedAsync(pipeline, ConnectionTimeout, cancellationToken)
                .ConfigureAwait(false);

            if (!connected)
            {
                pipeline.Dispose();
                return StartRecordingResult.Conflict(
                    $"Camera {parameters.CameraId} did not reach Connected within {ConnectionTimeout.TotalSeconds:N0}s.");
            }

            // Race window: between the IsRecording check above and here, a
            // concurrent caller (another REST
            // request) may have already started this camera. StartRecording
            // returns false on duplicate Camera.Id and does NOT take ownership
            // of our pipeline, so dispose it here to avoid a leak.
            if (!recordingService.StartRecording(camera, pipeline))
            {
                pipeline.Dispose();
                return StartRecordingResult.Conflict(
                    $"Camera {parameters.CameraId} is already recording (started by another caller).");
            }
        }
        catch
        {
            pipeline.Dispose();
            throw;
        }

        return StartRecordingResult.Ok(BuildStatus(parameters.CameraId));
    }

    private RecordingStatus BuildStatus(Guid cameraId)
    {
        var session = recordingService.GetSession(cameraId);
        var state = recordingService.GetRecordingState(cameraId);

        return new RecordingStatus(
            CameraId: cameraId,
            State: state.ToApiRecordingState(),
            FilePath: session?.CurrentFilePath ?? string.Empty,
            StartedAt: session is not null ? new DateTimeOffset(session.StartTime, TimeSpan.Zero) : DateTimeOffset.UtcNow);
    }
}