namespace AtcSoft.VideoSurveillance.Api.Domain.Webhooks;

/// <summary>
/// A receiver of the server's events, as clients see it. The secret that
/// signs the requests is never part of it.
/// </summary>
/// <param name="Id">Identifies the receiver.</param>
/// <param name="Url">The address events are posted to.</param>
/// <param name="Events">The events it gets, by name or by group ("motion.*"); empty for all.</param>
/// <param name="HasSecret">Whether its requests are signed.</param>
/// <param name="IsEditable">False for a receiver that comes from the server's configuration.</param>
[SuppressMessage(
    "Design",
    "CA1054:URI parameters should not be strings",
    Justification = "Text from a client or the configuration; it is validated when stored.")]
[SuppressMessage(
    "Design",
    "CA1056:URI properties should not be strings",
    Justification = "Text from a client or the configuration; it is validated when stored.")]
public sealed record WebhookRegistration(
    Guid Id,
    string Url,
    IReadOnlyList<string> Events,
    bool HasSecret,
    bool IsEditable);