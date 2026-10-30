namespace AtcSoft.CameraWall.Wpf.Services;

// Not auto-registered via [Registration]: the App registers it as a hosted service, so the
// Generic Host starts and stops it with the other workers.

/// <summary>
/// Runs the <see cref="ActivityIndexRecorder"/> in CameraWall, the same as on the server: an
/// activity index beside every recording, with each camera's own motion settings.
/// </summary>
/// <remarks>
/// Event-driven, on <see cref="BackgroundServiceBase{T}"/> like the other hosted workers:
/// <see cref="DoWorkAsync"/> is a no-op and the idle loop parks on a long delay.
/// </remarks>
public sealed class ActivityIndexService : BackgroundServiceBase<ActivityIndexService>
{
    private readonly ActivityIndexRecorder recorder;

    public ActivityIndexService(
        ILogger<ActivityIndexService> logger,
        IRecordingService recordingService,
        IMotionDetectionService motionDetectionService,
        ICameraStorageService storageService,
        IApplicationSettingsService settingsService)
        : base(logger, new DefaultBackgroundServiceOptions
        {
            ServiceName = nameof(ActivityIndexService),
            RepeatIntervalSeconds = ushort.MaxValue,
        })
    {
        ArgumentNullException.ThrowIfNull(storageService);
        ArgumentNullException.ThrowIfNull(settingsService);

        recorder = new ActivityIndexRecorder(
            logger,
            recordingService,
            motionDetectionService,
            cameraId => storageService.GetCameraById(cameraId) is { } camera
                ? Resolve(camera, settingsService.MotionDetection)
                : null,
            () => settingsService.Recording.EnableActivityIndex,
            cameraId => storageService.GetCameraById(cameraId)?.MotionZones ?? []);
    }

    /// <inheritdoc />
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        recorder.Start();
        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        recorder.Stop();
        return base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task DoWorkAsync(CancellationToken stoppingToken)
        => Task.CompletedTask; // Event-driven: the recorder works in the handlers it subscribes in Start.

    /// <inheritdoc />
    public override void Dispose()
    {
        recorder.Dispose();
        base.Dispose();
    }

    // The camera's own motion settings over the app's, as its tile resolves them.
    private static MotionDetectionSettings Resolve(
        CameraConfiguration camera,
        MotionDetectionSettings defaults)
    {
        var overrides = camera.Overrides?.MotionDetection;
        return new MotionDetectionSettings
        {
            Sensitivity = overrides?.Sensitivity ?? defaults.Sensitivity,
            SmallestObjectPercent = overrides?.SmallestObjectPercent ?? defaults.SmallestObjectPercent,
            StoppedObjectSeconds = overrides?.StoppedObjectSeconds ?? defaults.StoppedObjectSeconds,
            IgnoreLightingChanges = overrides?.IgnoreLightingChanges ?? defaults.IgnoreLightingChanges,
            MinimumChangePercent = overrides?.MinimumChangePercent ?? defaults.MinimumChangePercent,
            AnalysisFrameRate = overrides?.AnalysisFrameRate ?? defaults.AnalysisFrameRate,
            AnalysisWidth = overrides?.AnalysisWidth is > 0 and var ownWidth ? ownWidth : defaults.AnalysisWidth,
            PostMotionDurationSeconds = overrides?.PostMotionDurationSeconds ?? defaults.PostMotionDurationSeconds,
            CooldownSeconds = overrides?.CooldownSeconds ?? defaults.CooldownSeconds,
        };
    }
}