namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Options for <see cref="RecordingPreviewService"/>: how often it looks for a queued recording.
/// A few seconds after a recording ends, so its file and activity index are closed.
/// </summary>
public sealed class RecordingPreviewServiceOptions : IBackgroundServiceOptions
{
    /// <inheritdoc />
    public ushort StartupDelaySeconds { get; set; } = 10;

    /// <inheritdoc />
    public ushort RepeatIntervalSeconds { get; set; } = 5;

    /// <inheritdoc />
    public string? ServiceName { get; set; } = nameof(RecordingPreviewService);
}