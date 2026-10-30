namespace AtcSoft.VideoSurveillance.Api.Webhooks;

/// <summary>
/// The <c>Webhooks</c> section of the server's configuration.
/// </summary>
[SuppressMessage(
    "Usage",
    "CA2227:Collection properties should be read only",
    Justification = "Set by configuration binding.")]
public sealed class WebhookOptions
{
    /// <summary>
    /// The name of the configuration section.
    /// </summary>
    public const string SectionName = "Webhooks";

    /// <summary>
    /// Gets or sets the receivers. Without any, no webhook is sent.
    /// </summary>
    public IList<WebhookEndpoint> Endpoints { get; set; } = [];
}