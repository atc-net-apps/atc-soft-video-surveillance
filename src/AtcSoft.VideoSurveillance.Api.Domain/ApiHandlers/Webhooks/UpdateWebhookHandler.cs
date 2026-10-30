namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Webhooks;

/// <summary>
/// Handler business logic for the UpdateWebhook operation.
/// </summary>
public sealed class UpdateWebhookHandler(
    IWebhookRegistry registry) : IUpdateWebhookHandler
{
    public Task<UpdateWebhookResult> ExecuteAsync(
        UpdateWebhookParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var request = parameters.Request;
        var (outcome, webhook) = registry.Update(parameters.WebhookId, request.Url, request.Secret, request.Events);

        return Task.FromResult(outcome switch
        {
            WebhookChangeOutcome.Done when webhook is not null => UpdateWebhookResult.Ok(webhook.ToApiModel()),
            WebhookChangeOutcome.NotEditable => UpdateWebhookResult.Conflict("This receiver comes from the server's configuration and is changed there."),
            WebhookChangeOutcome.InvalidAddress => UpdateWebhookResult.BadRequest("A receiver needs an http or https address."),
            _ => UpdateWebhookResult.NotFound($"Receiver {parameters.WebhookId} not found."),
        });
    }
}