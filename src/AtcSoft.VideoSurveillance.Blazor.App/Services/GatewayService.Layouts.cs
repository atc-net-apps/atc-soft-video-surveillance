namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// Gateway service - Layouts operations using generated endpoints.
/// </summary>
public sealed partial class GatewayService
{
    public async Task<Layout[]?> GetLayoutsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await listLayoutsEndpoint
            .ExecuteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent.ToArray()
            : null;
    }

    public async Task<GatewayResult<Layout>> CreateLayoutAsync(
        CreateLayoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var parameters = new CreateLayoutParameters(Request: request);
        var result = await createLayoutEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsCreated
            ? GatewayResult.Success(result.CreatedContent)
            : GatewayResult.Refused<Layout>(result);
    }

    public async Task<GatewayResult<Layout>> UpdateLayoutAsync(
        Guid layoutId,
        UpdateLayoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var parameters = new UpdateLayoutParameters(LayoutId: layoutId, Request: request);
        var result = await updateLayoutEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? GatewayResult.Success(result.OkContent)
            : GatewayResult.Refused<Layout>(result);
    }

    public async Task<GatewayResult> DeleteLayoutAsync(
        Guid layoutId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new DeleteLayoutParameters(LayoutId: layoutId);
        var result = await deleteLayoutEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsNoContent
            ? GatewayResult.Done
            : GatewayResult.Refused(result);
    }

    public async Task<GatewayResult<Layout>> ApplyLayoutAsync(
        Guid layoutId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new ApplyLayoutParameters(LayoutId: layoutId);
        var result = await applyLayoutEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? GatewayResult.Success(result.OkContent)
            : GatewayResult.Refused<Layout>(result);
    }
}