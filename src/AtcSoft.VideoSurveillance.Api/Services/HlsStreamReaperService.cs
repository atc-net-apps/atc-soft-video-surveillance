namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Periodic worker that sweeps idle HLS stream sessions owned by
/// <see cref="StreamingService"/>. Extracted from the service's former internal
/// <see cref="System.Threading.Timer"/> so the sweep participates in the host
/// lifecycle (graceful shutdown, health reporting) via
/// <see cref="BackgroundServiceBase{T}"/>.
/// </summary>
public sealed class HlsStreamReaperService : BackgroundServiceBase<HlsStreamReaperService>
{
    private readonly StreamingService streamingService;

    public HlsStreamReaperService(
        ILogger<HlsStreamReaperService> logger,
        HlsStreamReaperServiceOptions options,
        IBackgroundServiceHealthService healthService,
        StreamingService streamingService)
        : base(logger, options, healthService)
    {
        this.streamingService = streamingService;

        healthService.SetMaxStalenessInSeconds(
            ServiceName,
            BackgroundServiceHealthHelper.StalenessFor(options));
    }

    /// <inheritdoc />
    public override Task DoWorkAsync(CancellationToken stoppingToken)
    {
        streamingService.ReapIdleSessions();
        return Task.CompletedTask;
    }
}