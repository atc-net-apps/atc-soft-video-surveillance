namespace AtcSoft.VideoSurveillance.Api.Domain.Mapping;

/// <summary>
/// Maps a receiver to the API's model.
/// </summary>
public static class WebhookMappingExtensions
{
    public static Webhook ToApiModel(this WebhookRegistration webhook)
    {
        ArgumentNullException.ThrowIfNull(webhook);

        return new Webhook(
            Id: webhook.Id,
            Url: webhook.Url,
            Events: [.. webhook.Events],
            HasSecret: webhook.HasSecret,
            IsEditable: webhook.IsEditable);
    }
}