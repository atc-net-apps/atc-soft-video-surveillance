namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Cameras;

/// <summary>
/// Handler business logic for the ListCameras operation.
/// </summary>
public sealed class ListCamerasHandler(
    ICameraStorageService storage) : IListCamerasHandler
{
    public Task<ListCamerasResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var cameras = storage
            .GetAllCameras()
            .Select(c => c.ToApiModel())
            .ToList();

        return Task.FromResult(ListCamerasResult.Ok(cameras));
    }
}