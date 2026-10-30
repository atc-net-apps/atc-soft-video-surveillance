namespace AtcSoft.CameraWall.Wpf.Services;

/// <summary>
/// When <see cref="MediaCleanupService"/> runs: shortly after start, then every hour. The free
/// space is kept every 30 seconds by the recording service's guard; this pass deletes what is
/// older than the retention and keeps the size limit.
/// </summary>
public sealed class MediaCleanupServiceOptions : IBackgroundServiceOptions
{
    public ushort StartupDelaySeconds { get; set; } = 5;

    public ushort RepeatIntervalSeconds { get; set; } = 60 * 60;

    public string? ServiceName { get; set; } = nameof(MediaCleanupService);
}