namespace AtcSoft.VideoSurveillance.Api.Webhooks;

/// <summary>
/// Something that happened to a camera, as it is sent to webhooks.
/// </summary>
/// <param name="Type">The event name, one of <see cref="WebhookEventTypes"/>.</param>
/// <param name="CameraId">The camera it happened to.</param>
/// <param name="CameraName">The camera's display name.</param>
/// <param name="Timestamp">When it happened.</param>
/// <param name="Data">What else is known about it; depends on the event (the specification's WebhookPayloadData).</param>
public sealed record WebhookEvent(
    string Type,
    Guid CameraId,
    string CameraName,
    DateTimeOffset Timestamp,
    WebhookPayloadData Data);