namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Options for <see cref="HlsStreamReaperService"/>. The 10s repeat mirrors the
/// reaper cadence previously hard-coded in <see cref="StreamingService"/> so the
/// worst-case lag from a dropped client to stream teardown stays ~70s.
/// </summary>
public sealed class HlsStreamReaperServiceOptions : IBackgroundServiceOptions
{
    /// <inheritdoc />
    public ushort StartupDelaySeconds { get; set; } = 10;

    /// <inheritdoc />
    public ushort RepeatIntervalSeconds { get; set; } = 10;

    /// <inheritdoc />
    public string? ServiceName { get; set; } = nameof(HlsStreamReaperService);
}