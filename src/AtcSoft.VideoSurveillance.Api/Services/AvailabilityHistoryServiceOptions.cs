namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Options for <see cref="AvailabilityHistoryService"/>. The changes are noted as they happen;
/// <see cref="RepeatIntervalSeconds"/> is how often the server notes that it is still running, so a
/// crash or a power cut is placed to within that time.
/// </summary>
public sealed class AvailabilityHistoryServiceOptions : IBackgroundServiceOptions
{
    /// <inheritdoc />
    public ushort StartupDelaySeconds { get; set; }

    /// <inheritdoc />
    public ushort RepeatIntervalSeconds { get; set; } = 30;

    /// <inheritdoc />
    public string? ServiceName { get; set; } = nameof(AvailabilityHistoryService);
}