namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Options for <see cref="SurveillanceEventBroadcaster"/>. Implements
/// <see cref="IBackgroundServiceOptions"/> so the typed instance can be passed
/// straight to <see cref="BackgroundServiceBase{T}"/> via DI without colliding
/// on the shared <see cref="IBackgroundServiceOptions"/> singleton.
/// <br/><br/>
/// The broadcaster is purely event-driven — it publishes Core events to GET /events
/// and has no periodic workload — so <see cref="RepeatIntervalSeconds"/> is set
/// to the maximum: the base-class idle loop parks on a long delay instead of
/// spinning, while subscription/tear-down happens in StartAsync/StopAsync.
/// </summary>
public sealed class SurveillanceEventBroadcasterOptions : IBackgroundServiceOptions
{
    /// <inheritdoc />
    public ushort StartupDelaySeconds { get; set; }

    /// <inheritdoc />
    public ushort RepeatIntervalSeconds { get; set; } = ushort.MaxValue;

    /// <inheritdoc />
    public string? ServiceName { get; set; } = nameof(SurveillanceEventBroadcaster);
}