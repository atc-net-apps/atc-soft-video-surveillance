namespace AtcSoft.Video.Surveillance.Api.Domain.ApiHandlers.Cameras;

/// <summary>
/// Handler business logic for the CaptureSnapshot operation.
/// Creates a transient media pipeline to capture a single frame from the
/// camera stream. A camera that can only be opened once (a USB camera) is
/// not opened here: the picture is taken from the one pipeline its
/// <see cref="ICameraPipelineHost"/> keeps, which someone may be watching
/// or recording on. Nor is a camera being recorded: its picture is taken
/// from the recording, so a camera that allows few sessions is not opened
/// a second time; nor is a camera the server watches for motion: its
/// picture is taken from the pipeline that watches it.
/// </summary>
public sealed class CaptureSnapshotHandler(
    ICameraStorageService storage,
    IMediaPipelineFactory pipelineFactory,
    IApplicationSettingsService settingsService,
    ICameraPipelineHost? pipelineHost = null,
    IRecordingService? recordings = null,
    ICameraRuntime? runtime = null) : ICaptureSnapshotHandler
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

        if (await CaptureFromAsync(runtime?.FindPlayingPipeline(camera.Id), cancellationToken).ConfigureAwait(false) is { } watched)
        {
            return await SaveAsync(watched, camera, cancellationToken).ConfigureAwait(false);
        }

        using var pipeline = pipelineFactory.Create(camera);

        // pipelineFactory.Create returns before the camera is open, and the player says it plays
        // before it has decoded its first picture: without a picture there is nothing to capture,
        // so the wait is for the first picture.
        var playing = await PipelineConnectionWaiter
            .WaitForPicturesAsync(pipeline, ConnectionTimeout, cancellationToken)
            .ConfigureAwait(false);
        if (!playing)
        {
            return CaptureSnapshotResult.ServiceUnavailable(
                $"Camera {parameters.CameraId} delivered no picture within {ConnectionTimeout.TotalSeconds:N0}s.", retryAfter: RetryAfterSeconds);
        }

        return await CaptureAndSaveAsync(pipeline, camera, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CaptureSnapshotResult> CaptureFromSharedPipelineAsync(
        ICameraPipelineHost host,
        AtcSoft.Video.Surveillance.Models.CameraConfiguration camera,
        CancellationToken cancellationToken)
    {
        var pipeline = host.Acquire(camera, AtcSoft.Video.Surveillance.Enums.CameraPipelineUse.Snapshot);
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
            host.Release(camera.Id, AtcSoft.Video.Surveillance.Enums.CameraPipelineUse.Snapshot);
        }
    }

    // The pipeline belongs to the recording and is never closed here. A recording that stops
    // meanwhile, or gives no picture, leaves the camera to be opened as usual.
    private Task<byte[]?> CaptureFromRecordingAsync(
        Guid cameraId,
        CancellationToken cancellationToken)
        => CaptureFromAsync(recordings?.FindPipeline(cameraId), cancellationToken);

    // A pipeline someone else owns: never closed here. One closed meanwhile gives no picture.
    private static async Task<byte[]?> CaptureFromAsync(
        IMediaPipeline? pipeline,
        CancellationToken cancellationToken)
    {
        if (pipeline is null)
        {
            return null;
        }

        try
        {
            return await pipeline.CaptureFrameAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }

    private async Task<CaptureSnapshotResult> CaptureAndSaveAsync(
        IMediaPipeline pipeline,
        AtcSoft.Video.Surveillance.Models.CameraConfiguration camera,
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
        AtcSoft.Video.Surveillance.Models.CameraConfiguration camera,
        CancellationToken cancellationToken)
    {
        var snapshotPath = settingsService.CameraDisplay.SnapshotPath;

        var safeName = RecordingPathHelper.GetCameraFolderName(camera.Display.DisplayName, camera.Id);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

        try
        {
            Directory.CreateDirectory(snapshotPath);

            // Two snapshots in the same second get a name each; the second
            // never overwrites the first.
            var filePath = UniqueFilename.EnsureUnique(Path.Combine(snapshotPath, $"{safeName}_{timestamp}.png"));

            await File.WriteAllBytesAsync(filePath, frame, cancellationToken).ConfigureAwait(false);

            return CaptureSnapshotResult.Ok(frame, "image/png", Path.GetFileName(filePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A full disk, a folder the server may not write to: the caller is
            // told why, as a ProblemDetails, not with a server error.
            return CaptureSnapshotResult.ServiceUnavailable(
                $"The snapshot could not be saved in the snapshot folder: {ex.Message}", retryAfter: RetryAfterSeconds);
        }
    }
}