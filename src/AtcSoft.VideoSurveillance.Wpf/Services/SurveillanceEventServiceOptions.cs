namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// Options for <see cref="SurveillanceEventService"/>, its own type so it does
/// not share the <see cref="IBackgroundServiceOptions"/> of another worker.
/// </summary>
/// <remarks>
/// One tick reads the event stream until the app stops, so the interval is only
/// the wait before it starts again after a handler failed.
/// </remarks>
public sealed class SurveillanceEventServiceOptions : IBackgroundServiceOptions
{
    /// <inheritdoc />
    public ushort StartupDelaySeconds { get; set; }

    /// <inheritdoc />
    public ushort RepeatIntervalSeconds { get; set; } = 5;

    /// <inheritdoc />
    public string? ServiceName { get; set; } = nameof(SurveillanceEventService);
}