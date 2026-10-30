namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Layouts;

/// <summary>
/// Handler business logic for the DeleteLayout operation.
/// </summary>
public sealed class DeleteLayoutHandler(
    ICameraStorageService storage) : IDeleteLayoutHandler
{
    public Task<DeleteLayoutResult> ExecuteAsync(
        DeleteLayoutParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var deleted = storage.DeleteLayout(parameters.LayoutId);
        if (!deleted)
        {
            return Task.FromResult(DeleteLayoutResult.NotFound($"Layout {parameters.LayoutId} not found."));
        }

        storage.Save();

        return Task.FromResult(DeleteLayoutResult.NoContent());
    }
}