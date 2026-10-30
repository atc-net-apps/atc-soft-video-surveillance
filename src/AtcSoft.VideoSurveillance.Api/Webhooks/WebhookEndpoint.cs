namespace AtcSoft.VideoSurveillance.Api.Webhooks;

/// <summary>
/// One receiver of webhooks, as configured under <c>Webhooks:Endpoints</c>.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1056:URI properties should not be strings",
    Justification = "Bound from configuration, where it is text; an invalid value must not stop the server from starting.")]
[SuppressMessage(
    "Usage",
    "CA2227:Collection properties should be read only",
    Justification = "Set by configuration binding.")]
public sealed class WebhookEndpoint
{
    private const string InvalidAddress = "(invalid address)";

    /// <summary>
    /// Gets or sets the address events are posted to (http or https).
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the shared secret the body is signed with. Optional.
    /// It is never logged.
    /// </summary>
    public string? Secret { get; set; }

    /// <summary>
    /// Gets or sets the events this receiver wants, by name ("motion.started")
    /// or by group ("motion.*"). Empty means all of them.
    /// </summary>
    public IList<string> Events { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether the address can be posted to.
    /// </summary>
    public bool IsValid
        => Uri.TryCreate(Url, UriKind.Absolute, out var uri) &&
           (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Gets the receiver as it is named in logs: scheme and host only.
    /// Webhook addresses often carry their secret in the path or the query.
    /// </summary>
    public string LogName
        => IsValid
            ? new Uri(Url).GetLeftPart(UriPartial.Authority)
            : InvalidAddress;

    /// <summary>
    /// Gets a value indicating whether this receiver wants an event.
    /// </summary>
    /// <param name="eventType">The event name.</param>
    /// <returns><c>true</c> when the event is to be sent here.</returns>
    public bool Wants(string eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        if (Events.Count == 0)
        {
            return true;
        }

        foreach (var wanted in Events)
        {
            if (string.IsNullOrWhiteSpace(wanted))
            {
                continue;
            }

            var pattern = wanted.Trim();
            if (pattern.EndsWith(".*", StringComparison.Ordinal))
            {
                if (eventType.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (string.Equals(pattern, eventType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}