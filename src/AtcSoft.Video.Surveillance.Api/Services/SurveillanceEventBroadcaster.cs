namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// Background service that subscribes to Core service events and publishes them
/// to the readers of GET /events via the <see cref="SurveillanceEventFeed"/>.
/// <br/><br/>
/// Built on <see cref="BackgroundServiceBase{T}"/> for consistency with the
/// other hosted workers (shared start/stop logging and health-running state),
/// even though the work itself is event-driven rather than periodic:
/// <see cref="DoWorkAsync"/> is a no-op and the base idle loop parks on a long
/// delay (see <see cref="SurveillanceEventBroadcasterOptions"/>). Subscription
/// happens in <see cref="StartAsync"/> and tear-down in <see cref="StopAsync"/>.
/// </summary>
public sealed partial class SurveillanceEventBroadcaster : BackgroundServiceBase<SurveillanceEventBroadcaster>
{
    private readonly IRecordingService recordingService;
    private readonly ServerRecordingService serverRecordingService;
    private readonly IMotionDetectionService motionDetectionService;
    private readonly IUsbCameraLifecycleCoordinator usbLifecycle;
    private readonly SurveillanceEventFeed feed;

    // The cameras in motion: the first event of a motion and its end are kept for clients that
    // resume, the boxes in between only go out live.
    private readonly ConcurrentDictionary<Guid, byte> moving = new();

    public SurveillanceEventBroadcaster(
        ILogger<SurveillanceEventBroadcaster> logger,
        SurveillanceEventBroadcasterOptions options,
        IRecordingService recordingService,
        ServerRecordingService serverRecordingService,
        IMotionDetectionService motionDetectionService,
        IUsbCameraLifecycleCoordinator usbLifecycle,
        SurveillanceEventFeed feed)
        : base(logger, options)
    {
        this.recordingService = recordingService;
        this.serverRecordingService = serverRecordingService;
        this.motionDetectionService = motionDetectionService;
        this.usbLifecycle = usbLifecycle;
        this.feed = feed;
    }

    /// <inheritdoc />
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        recordingService.RecordingStateChanged += OnRecordingStateChanged;
        motionDetectionService.MotionDetected += OnMotionDetected;
        serverRecordingService.CameraConnectionStateChanged += OnCameraConnectionStateChanged;
        usbLifecycle.StateChanged += OnUsbLifecycleChanged;

        LogBroadcasterStarted();

        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        recordingService.RecordingStateChanged -= OnRecordingStateChanged;
        motionDetectionService.MotionDetected -= OnMotionDetected;
        serverRecordingService.CameraConnectionStateChanged -= OnCameraConnectionStateChanged;
        usbLifecycle.StateChanged -= OnUsbLifecycleChanged;

        LogBroadcasterStopped();

        return base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task DoWorkAsync(CancellationToken stoppingToken)
        => Task.CompletedTask; // Event-driven: all work runs in the handlers wired up in StartAsync.

    // The events fire on the threads that raise them (the demux thread for
    // recording and motion): a failure here is logged, never thrown into them.
    private void OnRecordingStateChanged(
        object? sender,
        RecordingStateChangedEventArgs e)
    {
        try
        {
            feed.Publish(e.ToHubEvent().ToStreamEvent());
        }
        catch (Exception ex)
        {
            LogBroadcastRecordingFailed(ex, e.CameraId);
        }
    }

    private void OnMotionDetected(
        object? sender,
        MotionDetectedEventArgs e)
    {
        try
        {
            feed.Publish(e.ToHubEvent().ToStreamEvent(), IsChange(e));
        }
        catch (Exception ex)
        {
            LogBroadcastMotionFailed(ex, e.CameraId);
        }
    }

    // A motion's first event and its end are changes; the boxes in between are not.
    private bool IsChange(MotionDetectedEventArgs e)
    {
        if (e.IsMotionActive)
        {
            return moving.TryAdd(e.CameraId, 0);
        }

        moving.TryRemove(e.CameraId, out _);
        return true;
    }

    private void OnCameraConnectionStateChanged(
        Guid cameraId,
        ConnectionState newState)
    {
        try
        {
            feed.Publish(HubEventMapping.ToHubEvent(cameraId, newState, DateTimeOffset.UtcNow).ToStreamEvent());
        }
        catch (Exception ex)
        {
            LogBroadcastConnectionFailed(ex, cameraId);
        }
    }

    private void OnUsbLifecycleChanged(
        object? sender,
        UsbCameraLifecycleChangedEventArgs e)
    {
        try
        {
            feed.Publish(e.ToHubEvent(DateTimeOffset.UtcNow).ToStreamEvent());
        }
        catch (Exception ex)
        {
            LogBroadcastUsbLifecycleFailed(ex, e.CameraId);
        }
    }
}