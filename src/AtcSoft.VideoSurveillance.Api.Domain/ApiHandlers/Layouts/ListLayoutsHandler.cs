namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Layouts;

/// <summary>
/// Handler business logic for the ListLayouts operation.
/// </summary>
public sealed class ListLayoutsHandler(
    ICameraStorageService storage) : IListLayoutsHandler
{
    public Task<ListLayoutsResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var layouts = storage
            .GetAllLayouts()
            .Select(l => l.ToApiModel())
            .ToList();

        return Task.FromResult(ListLayoutsResult.Ok(layouts));
    }
}