namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Options for <see cref="ServerRecordingSegmentationService"/>. The 30s repeat
/// interval mirrors the original <see cref="System.Threading.Timer"/> cadence —
/// short enough to detect a clock-aligned slot boundary within one tick.
/// </summary>
public sealed class ServerRecordingSegmentationServiceOptions : IBackgroundServiceOptions
{
    /// <inheritdoc />
    public ushort StartupDelaySeconds { get; set; } = 5;

    /// <inheritdoc />
    public ushort RepeatIntervalSeconds { get; set; } = 30;

    /// <inheritdoc />
    public string? ServiceName { get; set; } = nameof(ServerRecordingSegmentationService);
}