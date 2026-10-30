namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Webhooks;

/// <summary>
/// Handler business logic for the TestWebhook operation.
/// </summary>
public sealed class TestWebhookHandler(
    IWebhookRegistry registry) : ITestWebhookHandler
{
    public async Task<TestWebhookResult> ExecuteAsync(
        TestWebhookParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var delivered = await registry.SendTestAsync(parameters.WebhookId, cancellationToken).ConfigureAwait(false);

        return delivered is null
            ? TestWebhookResult.NotFound($"Receiver {parameters.WebhookId} not found.")
            : TestWebhookResult.Ok(new WebhookTestResult(Delivered: delivered.Value));
    }
}