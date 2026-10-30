namespace AtcSoft.VideoSurveillance.Api.Webhooks;

/// <summary>
/// How often the webhook dispatcher empties its queue.
/// </summary>
public sealed class WebhookDispatcherServiceOptions : IBackgroundServiceOptions
{
    public ushort StartupDelaySeconds { get; set; } = 1;

    public ushort RepeatIntervalSeconds { get; set; } = 1;

    public string? ServiceName { get; set; } = nameof(WebhookDispatcherService);
}