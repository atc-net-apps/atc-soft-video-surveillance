namespace AtcSoft.CameraWall.Wpf.Services;

/// <summary>
/// Service for automatically segmenting recordings at clock-aligned interval boundaries
/// (e.g., every 15 minutes at :00, :15, :30, :45).
/// </summary>
// Not auto-registered via [Registration]: the App registers this explicitly so
// the SAME singleton instance is exposed both as IRecordingSegmentationService
// (for the UI: RecordingSegmented event, IsRunning) and as an IHostedService
// (for the Generic Host to drive its periodic DoWorkAsync loop and graceful stop).
public partial class RecordingSegmentationService : BackgroundServiceBase<RecordingSegmentationService>, IRecordingSegmentationService
{
    private readonly IApplicationSettingsService settingsService;
    private readonly IRecordingService recordingService;
    private readonly Lock lockObject = new();
    private (DateOnly Date, int Slot) lastProcessed;
    private bool isRunning;
    private bool started;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingSegmentationService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="settingsService">The application settings service.</param>
    /// <param name="recordingService">The recording service.</param>
    public RecordingSegmentationService(
        ILogger<RecordingSegmentationService> logger,
        IApplicationSettingsService settingsService,
        IRecordingService recordingService)
        : base(logger, new DefaultBackgroundServiceOptions
        {
            ServiceName = nameof(RecordingSegmentationService),
            StartupDelaySeconds = 5,
            RepeatIntervalSeconds = 30,
        })
    {
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        this.recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));

        // Seed the high-water mark to the current slot so an app restart
        // doesn't immediately segment a recording that just started. Guard the
        // interval: ComputeSlot throws on a non-positive value, and a throwing
        // ctor would take down host startup over a mere misconfiguration.
        var intervalMinutes = settingsService.Recording.MaxRecordingDurationMinutes;
        lastProcessed = intervalMinutes > 0
            ? RecordingSlotCalculator.ComputeSlot(DateTime.Now, intervalMinutes)
            : (DateOnly.MinValue, -1);
    }

    /// <inheritdoc/>
    public event EventHandler<RecordingSegmentedEventArgs>? RecordingSegmented;

    /// <inheritdoc/>
    public bool IsRunning
    {
        get
        {
            lock (lockObject)
            {
                return isRunning;
            }
        }
    }

    /// <summary>
    /// Periodic segmentation tick driven by the host. The
    /// <see cref="RecordingSettings.EnableHourlySegmentation"/> flag is re-read
    /// each pass, so toggling it at runtime takes effect without a restart.
    /// </summary>
    /// <param name="stoppingToken">The stopping token.</param>
    public override Task DoWorkAsync(CancellationToken stoppingToken)
    {
        // Disk guard runs independently of the segmentation setting so manual
        // recordings are protected even when hourly segmentation is disabled.
        recordingService.EnforceDiskSpaceGuard();

        var settings = settingsService.Recording;
        var enabled = settings.EnableHourlySegmentation;

        lock (lockObject)
        {
            isRunning = enabled;
        }

        if (!enabled)
        {
            return Task.CompletedTask;
        }

        if (!started)
        {
            LogSegmentationStarted();
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

        // Get all active sessions
        var activeSessions = recordingService.GetActiveSessions();

        if (activeSessions.Count == 0)
        {
            // Advance slot tracking even when no recordings so that idle
            // periods don't trigger a stale boundary on the next session
            if (isIntervalBoundary)
            {
                lastProcessed = currentSlot;
            }

            return;
        }

        foreach (var session in activeSessions)
        {
            var shouldSegment = false;
            var reason = SegmentationReason.IntervalBoundary;

            // Check for interval boundary (clock-aligned)
            if (isIntervalBoundary)
            {
                shouldSegment = true;
                reason = SegmentationReason.IntervalBoundary;
                LogIntervalBoundaryDetected(session.CameraName, currentSlot.Slot, intervalMinutes);
            }
            else if (session.Duration >= maxDuration)
            {
                shouldSegment = true;
                reason = SegmentationReason.MaxDurationReached;
                LogMaxDurationReached(session.CameraName, session.Duration);
            }

            if (shouldSegment)
            {
                var previousFilePath = session.CurrentFilePath;

                LogSegmentingRecording(session.CameraName, reason);

                var success = recordingService.SegmentRecording(session.CameraId);

                if (success)
                {
                    // Get the new session to find the new file path
                    var newSession = recordingService.GetSession(session.CameraId);
                    var newFilePath = newSession?.CurrentFilePath ?? string.Empty;

                    OnRecordingSegmented(session.CameraId, previousFilePath, newFilePath, reason);
                }
                else
                {
                    LogSegmentingFailed(session.CameraName);
                }
            }
        }

        // Only advance the high-water mark forward; backward clock jumps
        // (NTP rollback, DST fall-back) leave it untouched so the same
        // wall-clock slot is never segmented twice
        if (isIntervalBoundary)
        {
            lastProcessed = currentSlot;
        }
    }

    private void OnRecordingSegmented(
        Guid cameraId,
        string previousFilePath,
        string newFilePath,
        SegmentationReason reason)
    {
        RecordingSegmented?.Invoke(
            this,
            new RecordingSegmentedEventArgs(cameraId, previousFilePath, newFilePath, reason));
    }
}