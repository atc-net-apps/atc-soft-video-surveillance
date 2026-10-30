namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Webhooks;

/// <summary>
/// Handler business logic for the ListWebhooks operation.
/// </summary>
public sealed class ListWebhooksHandler(
    IWebhookRegistry registry) : IListWebhooksHandler
{
    public Task<ListWebhooksResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(ListWebhooksResult.Ok([.. registry.GetAll().Select(w => w.ToApiModel())]));
}