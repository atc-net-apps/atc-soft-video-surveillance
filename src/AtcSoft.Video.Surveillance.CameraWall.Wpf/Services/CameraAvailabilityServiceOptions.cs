namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

/// <summary>
/// When <see cref="CameraAvailabilityService"/> notes that the app is alive: every minute, so a
/// stop without a word (a crash, a power cut) is placed within a minute.
/// </summary>
public sealed class CameraAvailabilityServiceOptions : IBackgroundServiceOptions
{
    public ushort StartupDelaySeconds { get; set; } = 5;

    public ushort RepeatIntervalSeconds { get; set; } = 60;

    public string? ServiceName { get; set; } = nameof(CameraAvailabilityService);
}