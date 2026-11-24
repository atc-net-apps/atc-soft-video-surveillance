namespace AtcSoft.Video.Surveillance.Api.Domain.ApiHandlers.Cameras;

/// <summary>
/// Handler business logic for the ListCameras operation.
/// </summary>
public sealed class ListCamerasHandler(
    ICameraStorageService storage,
    SubStreamAvailability? subStreams = null) : IListCamerasHandler
{
    public Task<ListCamerasResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var cameras = storage
            .GetAllCameras()
            .Select(c => c.ToApiModel(subStreamFailing: subStreams?.IsFailing(c) == true))
            .ToList();

        return Task.FromResult(ListCamerasResult.Ok(cameras));
    }
}