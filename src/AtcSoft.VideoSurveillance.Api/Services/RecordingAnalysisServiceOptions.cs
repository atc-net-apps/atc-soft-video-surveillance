namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Options for <see cref="RecordingAnalysisService"/>: how often it looks for a queued recording.
/// </summary>
public sealed class RecordingAnalysisServiceOptions : IBackgroundServiceOptions
{
    /// <inheritdoc />
    public ushort StartupDelaySeconds { get; set; } = 5;

    /// <inheritdoc />
    public ushort RepeatIntervalSeconds { get; set; } = 2;

    /// <inheritdoc />
    public string? ServiceName { get; set; } = nameof(RecordingAnalysisService);
}