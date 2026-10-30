namespace AtcSoft.VideoSurveillance.Api.Host;

/// <summary>
/// One webhook as it arrived at the receiver.
/// </summary>
public sealed record ReceivedWebhook(
    string EventType,
    string Signature,
    string Body);