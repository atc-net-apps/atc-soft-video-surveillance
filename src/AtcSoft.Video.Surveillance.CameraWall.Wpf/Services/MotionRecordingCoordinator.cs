namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

// Not auto-registered via [Registration]: the App registers it as a hosted service, so the
// Generic Host starts and stops it with the other workers.

/// <summary>
/// Turns motion into recordings for every camera that records on motion, shown in a tile or not: a
/// camera the shown layout leaves out goes on being watched (its player is parked with its
/// detection running), and its motion is recorded from the pipeline it is watched on. Before, only
/// a tile started a motion recording, so nothing was recorded on motion while another layout was shown.
/// </summary>
/// <remarks>
/// Event-driven, on <see cref="BackgroundServiceBase{T}"/> like the other hosted workers:
/// <see cref="DoWorkAsync"/> is a no-op and the idle loop parks on a long delay. A tile that shows the
/// camera asks for the same recording; the second call only moves the time of the last motion.
/// </remarks>
public sealed partial class MotionRecordingCoordinator : BackgroundServiceBase<MotionRecordingCoordinator>
{
    private readonly IMotionDetectionService motionDetectionService;
    private readonly IRecordingService recordingService;
    private readonly ICameraStorageService storageService;
    private readonly IApplicationSettingsService settingsService;

    public MotionRecordingCoordinator(
        ILogger<MotionRecordingCoordinator> logger,
        IMotionDetectionService motionDetectionService,
        IRecordingService recordingService,
        ICameraStorageService storageService,
        IApplicationSettingsService settingsService)
        : base(logger, new DefaultBackgroundServiceOptions
        {
            ServiceName = nameof(MotionRecordingCoordinator),
            RepeatIntervalSeconds = ushort.MaxValue,
        })
    {
        this.motionDetectionService = motionDetectionService ?? throw new ArgumentNullException(nameof(motionDetectionService));
        this.recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        this.storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    /// <inheritdoc />
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        motionDetectionService.MotionDetected += OnMotionDetected;
        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        motionDetectionService.MotionDetected -= OnMotionDetected;
        return base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task DoWorkAsync(CancellationToken stoppingToken)
        => Task.CompletedTask; // Event-driven: the work is in OnMotionDetected.

    /// <summary>
    /// Starts or extends the recording of a camera that sees motion, when it records on motion.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <returns><see langword="true"/> when a recording runs for the motion.</returns>
    internal bool RecordMotion(Guid cameraId)
    {
        if (storageService.GetCameraById(cameraId) is not { } camera ||
            !(camera.Overrides?.Recording.EnableRecordingOnMotion ?? settingsService.Recording.EnableRecordingOnMotion) ||
            motionDetectionService.FindPipeline(cameraId) is not { } pipeline)
        {
            return false;
        }

        return recordingService.TriggerMotionRecording(camera, pipeline);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Raised for every analysed picture: a failure is logged and the next picture tries again.")]
    private void OnMotionDetected(
        object? sender,
        MotionDetectedEventArgs e)
    {
        if (!e.IsMotionActive)
        {
            return;
        }

        try
        {
            _ = RecordMotion(e.CameraId);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogRecordMotionFailed(ex, e.CameraId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recording the motion of camera {CameraId} failed")]
    private partial void LogRecordMotionFailed(Exception ex, Guid cameraId);
}