namespace AtcSoft.VideoSurveillance.Api.Webhooks;

/// <summary>
/// Delivers one event to one receiver.
/// </summary>
public interface IWebhookSender
{
    /// <summary>
    /// Posts the event. Never throws for a receiver that fails or cannot be reached.
    /// </summary>
    /// <param name="endpoint">The receiver.</param>
    /// <param name="webhookEvent">The event.</param>
    /// <param name="cancellationToken">Stops the delivery, for instance at shutdown.</param>
    /// <returns><c>true</c> when the receiver accepted the event.</returns>
    Task<bool> SendAsync(
        WebhookEndpoint endpoint,
        WebhookEvent webhookEvent,
        CancellationToken cancellationToken);
}