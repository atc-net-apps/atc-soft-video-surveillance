namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Layouts;

/// <summary>
/// Handler business logic for the ApplyLayout operation.
/// Sets the layout as the active/startup layout.
/// </summary>
public sealed class ApplyLayoutHandler(
    ICameraStorageService storage) : IApplyLayoutHandler
{
    public Task<ApplyLayoutResult> ExecuteAsync(
        ApplyLayoutParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var layout = storage.GetLayoutById(parameters.LayoutId);
        if (layout is null)
        {
            return Task.FromResult(ApplyLayoutResult.NotFound($"Layout {parameters.LayoutId} not found."));
        }

        storage.StartupLayoutId = layout.Id;
        storage.Save();

        return Task.FromResult(ApplyLayoutResult.Ok(layout.ToApiModel()));
    }
}