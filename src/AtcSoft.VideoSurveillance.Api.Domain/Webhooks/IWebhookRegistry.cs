namespace AtcSoft.VideoSurveillance.Api.Domain.Webhooks;

/// <summary>
/// The receivers the server posts its events to.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1054:URI parameters should not be strings",
    Justification = "Text from a client; it is validated here.")]
public interface IWebhookRegistry
{
    /// <summary>
    /// Gets all receivers, those from the configuration first.
    /// </summary>
    IReadOnlyList<WebhookRegistration> GetAll();

    /// <summary>
    /// Adds a receiver and stores it.
    /// </summary>
    /// <param name="url">The http or https address to post to.</param>
    /// <param name="secret">Signs the requests; null or empty for unsigned requests.</param>
    /// <param name="events">The events it gets; null or empty for all.</param>
    (WebhookChangeOutcome Outcome, WebhookRegistration? Webhook) Add(
        string url,
        string? secret,
        IReadOnlyList<string>? events);

    /// <summary>
    /// Changes a receiver that was added here.
    /// </summary>
    /// <param name="id">The receiver.</param>
    /// <param name="url">The http or https address to post to.</param>
    /// <param name="secret">Null keeps the secret it has, an empty text removes it, any other replaces it.</param>
    /// <param name="events">The events it gets; null or empty for all.</param>
    (WebhookChangeOutcome Outcome, WebhookRegistration? Webhook) Update(
        Guid id,
        string url,
        string? secret,
        IReadOnlyList<string>? events);

    /// <summary>
    /// Removes a receiver that was added here.
    /// </summary>
    WebhookChangeOutcome Delete(Guid id);

    /// <summary>
    /// Sends a test event to one receiver.
    /// </summary>
    /// <returns>Whether the receiver took it; null when there is no such receiver.</returns>
    Task<bool?> SendTestAsync(
        Guid id,
        CancellationToken cancellationToken);
}