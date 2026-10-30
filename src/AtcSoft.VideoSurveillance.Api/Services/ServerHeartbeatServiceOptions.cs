namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Options for <see cref="ServerHeartbeatService"/>. Implements
/// <see cref="IBackgroundServiceOptions"/> so the typed instance can be
/// passed straight to <see cref="BackgroundServiceBase{T}"/> via DI without
/// colliding on the shared <see cref="IBackgroundServiceOptions"/> singleton
/// already used by <see cref="CameraConnectionService"/>.
/// </summary>
public sealed class ServerHeartbeatServiceOptions : IBackgroundServiceOptions
{
    /// <inheritdoc />
    public ushort StartupDelaySeconds { get; set; } = 30;

    /// <inheritdoc />
    public ushort RepeatIntervalSeconds { get; set; } = 60 * 60;

    /// <inheritdoc />
    public string? ServiceName { get; set; } = nameof(ServerHeartbeatService);
}