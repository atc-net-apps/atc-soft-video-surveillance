namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Server-side periodic segmentation. Mirrors the WPF
/// <c>RecordingSegmentationService</c>, driven by
/// <see cref="BackgroundServiceBase{T}"/> (sequential, no-overlap ticks) and
/// the shared <see cref="RecordingSlotCalculator"/> for boundary detection that
/// is immune to NTP rollback, midnight, and DST.
/// </summary>
public sealed partial class ServerRecordingSegmentationService : BackgroundServiceBase<ServerRecordingSegmentationService>
{
    private readonly IApplicationSettingsService settingsService;
    private readonly IRecordingService recordingService;

    private (DateOnly Date, int Slot) lastProcessed;
    private bool started;

    public ServerRecordingSegmentationService(
        ILogger<ServerRecordingSegmentationService> logger,
        ServerRecordingSegmentationServiceOptions options,
        IBackgroundServiceHealthService healthService,
        IApplicationSettingsService settingsService,
        IRecordingService recordingService)
        : base(logger, options, healthService)
    {
        this.settingsService = settingsService;
        this.recordingService = recordingService;

        // Seed the high-water mark to the current slot so a service restart
        // doesn't immediately segment a recording that just started. Guard the
        // interval: ComputeSlot throws on a non-positive value, and a throwing
        // ctor would take down host startup over a mere misconfiguration.
        var intervalMinutes = settingsService.Recording.MaxRecordingDurationMinutes;
        lastProcessed = intervalMinutes > 0
            ? RecordingSlotCalculator.ComputeSlot(DateTime.Now, intervalMinutes)
            : (DateOnly.MinValue, -1);

        healthService.SetMaxStalenessInSeconds(
            ServiceName,
            BackgroundServiceHealthHelper.StalenessFor(options));
    }

    /// <inheritdoc />
    public override Task DoWorkAsync(CancellationToken stoppingToken)
    {
        var settings = settingsService.Recording;

        // Disk guard runs independently of the segmentation setting so manual
        // recordings are protected even when hourly segmentation is disabled.
        recordingService.EnforceDiskSpaceGuard();

        if (!settings.EnableHourlySegmentation)
        {
            // Segmentation can be toggled at runtime; a disabled tick is a
            // cheap no-op (vs. the old design that only read the flag once at
            // startup and required a restart to pick up changes).
            return Task.CompletedTask;
        }

        if (!started)
        {
            LogSegmentationStarted(settings.MaxRecordingDurationMinutes);
            started = true;
        }

        PerformSegmentationCheck(settings);
        return Task.CompletedTask;
    }

    private void PerformSegmentationCheck(RecordingSettings settings)
    {
        var now = DateTime.Now;
        var intervalMinutes = settings.MaxRecordingDurationMinutes;
        var currentSlot = RecordingSlotCalculator.ComputeSlot(now, intervalMinutes);
        var isIntervalBoundary = RecordingSlotCalculator.IsNewBoundary(currentSlot, lastProcessed);
        var maxDuration = TimeSpan.FromMinutes(intervalMinutes);

        var activeSessions = recordingService.GetActiveSessions();
        if (activeSessions.Count == 0)
        {
            if (isIntervalBoundary)
            {
                lastProcessed = currentSlot;
            }

            return;
        }

        foreach (var session in activeSessions)
        {
            var shouldSegment = false;

            if (isIntervalBoundary)
            {
                shouldSegment = true;
                LogIntervalBoundaryDetected(session.CameraId, currentSlot.Slot, intervalMinutes);
            }
            else if (session.Duration >= maxDuration)
            {
                shouldSegment = true;
                LogMaxDurationReached(session.CameraId, session.Duration);
            }

            if (!shouldSegment)
            {
                continue;
            }

            var success = recordingService.SegmentRecording(session.CameraId);
            if (!success)
            {
                LogSegmentingFailed(session.CameraId);
            }
        }

        // Only advance the high-water mark forward; backward clock jumps
        // (NTP rollback, DST fall-back) leave it untouched so the same
        // wall-clock slot is never segmented twice.
        if (isIntervalBoundary)
        {
            lastProcessed = currentSlot;
        }
    }
}