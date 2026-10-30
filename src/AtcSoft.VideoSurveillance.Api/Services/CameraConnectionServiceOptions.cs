namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Options for <see cref="CameraConnectionService"/>. A dedicated type (rather
/// than the shared <see cref="IBackgroundServiceOptions"/> singleton) so each
/// <see cref="BackgroundServiceBase{T}"/>-derived service in the host can run
/// on its own interval without colliding in DI.
/// </summary>
public sealed class CameraConnectionServiceOptions : IBackgroundServiceOptions
{
    /// <inheritdoc />
    public ushort StartupDelaySeconds { get; set; } = 3;

    /// <inheritdoc />
    public ushort RepeatIntervalSeconds { get; set; } = 30;

    /// <inheritdoc />
    public string? ServiceName { get; set; } = nameof(CameraConnectionService);
}