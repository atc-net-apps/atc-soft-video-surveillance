namespace AtcSoft.Video.Surveillance.Api.Domain.ApiHandlers.Cameras;

/// <summary>
/// Handler business logic for the UpdateCamera operation. The update is made on a copy, which
/// then takes the stored camera's place: a recording, a pipeline or a save that reads the camera
/// meanwhile sees it either as it was or as it is, never half-updated. A camera being analysed
/// takes its new motion zones at once (edited, or turned with the picture). A change to how the
/// camera is reached or recorded (address, sign-in, stream options, recording codec) closes what
/// runs for it, which is opened again with the new settings.
/// </summary>
public sealed class UpdateCameraHandler(
    ICameraStorageService storage,
    IMotionDetectionService? motionDetection = null,
    ICameraRuntime? runtime = null,
    ILiveStreamService? liveStreams = null) : IUpdateCameraHandler
{
    public Task<UpdateCameraResult> ExecuteAsync(
        UpdateCameraParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var camera = storage.GetCameraById(parameters.CameraId);
        if (camera is null)
        {
            return Task.FromResult(UpdateCameraResult.NotFound($"Camera {parameters.CameraId} not found."));
        }

        var updated = camera.Clone();
        updated.ApplyUpdate(parameters.Request);
        if (CameraSourceCheck.Problem(updated) is { } problem)
        {
            return Task.FromResult(UpdateCameraResult.BadRequest(problem));
        }

        storage.AddOrUpdateCamera(updated);
        storage.Save();
        motionDetection?.UseZones(updated.Id, updated.MotionZones);

        if (CameraPipelineChange.NeedsReopen(camera, updated))
        {
            runtime?.Reopen(updated.Id);
            liveStreams?.EndStream(updated.Id);
        }

        return Task.FromResult(UpdateCameraResult.Ok(updated.ToApiModel()));
    }
}