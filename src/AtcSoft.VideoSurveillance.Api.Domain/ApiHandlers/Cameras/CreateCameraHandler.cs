namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Cameras;

/// <summary>
/// Handler business logic for the CreateCamera operation.
/// </summary>
public sealed class CreateCameraHandler(
    ICameraStorageService storage) : ICreateCameraHandler
{
    public Task<CreateCameraResult> ExecuteAsync(
        CreateCameraParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var camera = parameters.Request.ToCoreModel();
        if (CameraSourceCheck.Problem(camera) is { } problem)
        {
            return Task.FromResult(CreateCameraResult.BadRequest(problem));
        }

        storage.AddOrUpdateCamera(camera);
        storage.Save();

        return Task.FromResult(CreateCameraResult.Created(camera.ToApiModel(), location: $"/cameras/{camera.Id}"));
    }
}