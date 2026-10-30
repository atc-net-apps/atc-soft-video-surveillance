namespace AtcSoft.CameraWall.Wpf.Services;

// Not auto-registered via [Registration]: the App registers one instance both as
// IRecordingPreviews and as a hosted service, so the Generic Host starts and stops it.

/// <summary>
/// Makes the preview pictures of recordings in CameraWall, the same as on the server
/// (<see cref="RecordingPreviewMaker"/>): each recording or segment that ends, each one analysed,
/// and each one played that has none, one at a time in the background.
/// </summary>
public sealed class RecordingPreviewService : BackgroundServiceBase<RecordingPreviewService>, IRecordingPreviews
{
    private readonly RecordingPreviewMaker maker;

    public RecordingPreviewService(
        ILogger<RecordingPreviewService> logger,
        IRecordingService recordingService,
        IRecordingPreviewBuilder? builder = null)
        : base(logger, new DefaultBackgroundServiceOptions
        {
            ServiceName = nameof(RecordingPreviewService),
            StartupDelaySeconds = 10,
            RepeatIntervalSeconds = 5,
        })
    {
        maker = new RecordingPreviewMaker(logger, recordingService, builder);
    }

    /// <inheritdoc />
    public bool TryEnqueue(string videoPath)
        => maker.TryEnqueue(videoPath);

    /// <inheritdoc />
    public bool Contains(string videoPath)
        => maker.Contains(videoPath);

    /// <inheritdoc />
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        maker.Start();
        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        maker.Stop();
        return base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task DoWorkAsync(CancellationToken stoppingToken)
    {
        maker.RunPending(stoppingToken);
        return Task.CompletedTask;
    }
}