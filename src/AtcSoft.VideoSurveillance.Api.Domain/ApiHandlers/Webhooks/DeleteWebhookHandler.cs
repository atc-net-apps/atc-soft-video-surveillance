namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Webhooks;

/// <summary>
/// Handler business logic for the DeleteWebhook operation.
/// </summary>
public sealed class DeleteWebhookHandler(
    IWebhookRegistry registry) : IDeleteWebhookHandler
{
    public Task<DeleteWebhookResult> ExecuteAsync(
        DeleteWebhookParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return Task.FromResult(registry.Delete(parameters.WebhookId) switch
        {
            WebhookChangeOutcome.Done => DeleteWebhookResult.NoContent(),
            WebhookChangeOutcome.NotEditable => DeleteWebhookResult.Conflict("This receiver comes from the server's configuration and is removed there."),
            _ => DeleteWebhookResult.NotFound($"Receiver {parameters.WebhookId} not found."),
        });
    }
}