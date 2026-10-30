namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Cameras;

/// <summary>
/// Handler business logic for the GetCameraById operation.
/// </summary>
public sealed class GetCameraByIdHandler(
    ICameraStorageService storage) : IGetCameraByIdHandler
{
    public Task<GetCameraByIdResult> ExecuteAsync(
        GetCameraByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var camera = storage.GetCameraById(parameters.CameraId);
        if (camera is null)
        {
            return Task.FromResult(GetCameraByIdResult.NotFound($"Camera {parameters.CameraId} not found."));
        }

        return Task.FromResult(GetCameraByIdResult.Ok(camera.ToApiModel()));
    }
}