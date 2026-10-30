namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Cameras;

/// <summary>
/// Handler business logic for the UpdateCamera operation. A camera being analysed takes its new
/// motion zones at once (edited, or turned with the picture).
/// </summary>
public sealed class UpdateCameraHandler(
    ICameraStorageService storage,
    IMotionDetectionService? motionDetection = null) : IUpdateCameraHandler
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

        // Checked on a copy first, so a refused update leaves the stored camera as it was;
        // an accepted one is then applied to the stored camera itself.
        var updated = camera.Clone();
        updated.ApplyUpdate(parameters.Request);
        if (CameraSourceCheck.Problem(updated) is { } problem)
        {
            return Task.FromResult(UpdateCameraResult.BadRequest(problem));
        }

        camera.ApplyUpdate(parameters.Request);
        storage.AddOrUpdateCamera(camera);
        storage.Save();
        motionDetection?.UseZones(camera.Id, camera.MotionZones);

        return Task.FromResult(UpdateCameraResult.Ok(camera.ToApiModel()));
    }
}