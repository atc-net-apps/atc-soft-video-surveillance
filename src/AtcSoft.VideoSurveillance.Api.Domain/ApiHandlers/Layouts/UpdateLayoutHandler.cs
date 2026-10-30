namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Layouts;

/// <summary>
/// Handler business logic for the UpdateLayout operation.
/// </summary>
public sealed class UpdateLayoutHandler(
    ICameraStorageService storage) : IUpdateLayoutHandler
{
    public Task<UpdateLayoutResult> ExecuteAsync(
        UpdateLayoutParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var layout = storage.GetLayoutById(parameters.LayoutId);
        if (layout is null)
        {
            return Task.FromResult(UpdateLayoutResult.NotFound($"Layout {parameters.LayoutId} not found."));
        }

        layout.ApplyUpdate(parameters.Request);
        storage.AddOrUpdateLayout(layout);
        storage.Save();

        return Task.FromResult(UpdateLayoutResult.Ok(layout.ToApiModel()));
    }
}