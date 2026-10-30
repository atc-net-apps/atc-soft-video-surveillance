namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Cameras;

/// <summary>
/// Handler business logic for the DeleteCamera operation.
/// </summary>
public sealed class DeleteCameraHandler(
    ICameraStorageService storage,
    IUsbCameraLifecycleCoordinator lifecycleCoordinator) : IDeleteCameraHandler
{
    public Task<DeleteCameraResult> ExecuteAsync(
        DeleteCameraParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var deleted = storage.DeleteCamera(parameters.CameraId);
        if (!deleted)
        {
            return Task.FromResult(DeleteCameraResult.NotFound($"Camera {parameters.CameraId} not found."));
        }

        storage.Save();

        // Drop any unplugged-state entry the coordinator still holds.
        // Cheap no-op for network cameras and for USB cameras that were
        // plugged in at delete-time; for USB cameras deleted while
        // unplugged this is the only thing that keeps the coordinator's
        // ConcurrentDictionary from leaking an entry per delete.
        lifecycleCoordinator.ClearUnpluggedState(parameters.CameraId);

        return Task.FromResult(DeleteCameraResult.NoContent());
    }
}