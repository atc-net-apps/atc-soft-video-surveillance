namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Cameras;

/// <summary>
/// Handler business logic for the CaptureSnapshot operation.
/// Creates a transient media pipeline to capture a single frame from the
/// camera stream. A camera that can only be opened once (a USB camera) is
/// not opened here: the picture is taken from the one pipeline its
/// <see cref="ICameraPipelineHost"/> keeps, which someone may be watching
/// or recording on. Nor is a camera being recorded: its picture is taken
/// from the recording, so a camera that allows few sessions is not opened
/// a second time.
/// </summary>
public sealed class CaptureSnapshotHandler(
    ICameraStorageService storage,
    IMediaPipelineFactory pipelineFactory,
    IApplicationSettingsService settingsService,
    ICameraPipelineHost? pipelineHost = null,
    IRecordingService? recordings = null) : ICaptureSnapshotHandler
{
    // A camera that is still connecting usually has a picture a few seconds later.
    private const int RetryAfterSeconds = 5;

    // Same envelope as StartRecording — long enough for a typical RTSP open
    // (1-3s), short enough that a stuck client gets a definite answer.
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(15);

    public async Task<CaptureSnapshotResult> ExecuteAsync(
        CaptureSnapshotParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var camera = storage.GetCameraById(parameters.CameraId);
        if (camera is null)
        {
            return CaptureSnapshotResult.NotFound($"Camera {parameters.CameraId} not found.");
        }

        if (pipelineHost?.Hosts(camera) == true)
        {
            return await CaptureFromSharedPipelineAsync(pipelineHost, camera, cancellationToken).ConfigureAwait(false);
        }

        if (await CaptureFromRecordingAsync(camera.Id, cancellationToken).ConfigureAwait(false) is { } recorded)
        {
            return await SaveAsync(recorded, camera, cancellationToken).ConfigureAwait(false);
        }

        using var pipeline = pipelineFactory.Create(camera);

        // pipelineFactory.Create returns before RTSP open completes; the
        // player is in Opening state. CaptureFrameAsync returns null when
        // state != Playing, so without this wait the handler reports
        // "Failed to capture frame" on practically every cold call.
        var connected = await PipelineConnectionWaiter
            .WaitForConnectedAsync(pipeline, ConnectionTimeout, cancellationToken)
            .ConfigureAwait(false);
        if (!connected)
        {
            return CaptureSnapshotResult.ServiceUnavailable(
                $"Camera {parameters.CameraId} did not reach Connected within {ConnectionTimeout.TotalSeconds:N0}s.", retryAfter: RetryAfterSeconds);
        }

        return await CaptureAndSaveAsync(pipeline, camera, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CaptureSnapshotResult> CaptureFromSharedPipelineAsync(
        ICameraPipelineHost host,
        AtcSoft.VideoSurveillance.Models.CameraConfiguration camera,
        CancellationToken cancellationToken)
    {
        var pipeline = host.Acquire(camera, AtcSoft.VideoSurveillance.Enums.CameraPipelineUse.Snapshot);
        if (pipeline is null)
        {
            return CaptureSnapshotResult.ServiceUnavailable(
                "This USB camera cannot be opened right now: it is unplugged, or the server is still connecting to it. Try again in a moment.", retryAfter: RetryAfterSeconds);
        }

        // The pipeline is given back, never closed.
        try
        {
            var playing = await PipelineConnectionWaiter
                .WaitForPicturesAsync(pipeline, ConnectionTimeout, cancellationToken)
                .ConfigureAwait(false);
            if (!playing)
            {
                return CaptureSnapshotResult.ServiceUnavailable(
                    $"Camera {camera.Id} delivered no picture within {ConnectionTimeout.TotalSeconds:N0}s.", retryAfter: RetryAfterSeconds);
            }

            return await CaptureAndSaveAsync(pipeline, camera, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            host.Release(camera.Id, AtcSoft.VideoSurveillance.Enums.CameraPipelineUse.Snapshot);
        }
    }

    // The pipeline belongs to the recording and is never closed here. A recording that stops
    // meanwhile, or gives no picture, leaves the camera to be opened as usual.
    private async Task<byte[]?> CaptureFromRecordingAsync(
        Guid cameraId,
        CancellationToken cancellationToken)
    {
        if (recordings?.FindPipeline(cameraId) is not { } recording)
        {
            return null;
        }

        try
        {
            return await recording.CaptureFrameAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }

    private async Task<CaptureSnapshotResult> CaptureAndSaveAsync(
        IMediaPipeline pipeline,
        AtcSoft.VideoSurveillance.Models.CameraConfiguration camera,
        CancellationToken cancellationToken)
    {
        var frame = await pipeline.CaptureFrameAsync(cancellationToken);
        if (frame is null)
        {
            return CaptureSnapshotResult.ServiceUnavailable("Failed to capture frame from camera stream.", retryAfter: RetryAfterSeconds);
        }

        return await SaveAsync(frame, camera, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CaptureSnapshotResult> SaveAsync(
        byte[] frame,
        AtcSoft.VideoSurveillance.Models.CameraConfiguration camera,
        CancellationToken cancellationToken)
    {
        var snapshotPath = settingsService.CameraDisplay.SnapshotPath;
        Directory.CreateDirectory(snapshotPath);

        var safeName = RecordingPathHelper.GetCameraFolderName(camera.Display.DisplayName, camera.Id);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var fileName = $"{safeName}_{timestamp}.png";
        var filePath = Path.Combine(snapshotPath, fileName);

        await File.WriteAllBytesAsync(filePath, frame, cancellationToken);

        return CaptureSnapshotResult.Ok(frame, "image/png", fileName);
    }
}