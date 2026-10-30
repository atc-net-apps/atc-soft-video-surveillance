namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Options for <see cref="UsbDeviceCacheEvictor"/>, its own typed instance so it
/// does not share the <see cref="IBackgroundServiceOptions"/> singleton. The
/// evictor is purely event-driven, so <see cref="RepeatIntervalSeconds"/> is the
/// maximum: the base-class idle loop parks on a long delay instead of spinning.
/// </summary>
public sealed class UsbDeviceCacheEvictorOptions : IBackgroundServiceOptions
{
    /// <inheritdoc />
    public ushort StartupDelaySeconds { get; set; }

    /// <inheritdoc />
    public ushort RepeatIntervalSeconds { get; set; } = ushort.MaxValue;

    /// <inheritdoc />
    public string? ServiceName { get; set; } = nameof(UsbDeviceCacheEvictor);
}