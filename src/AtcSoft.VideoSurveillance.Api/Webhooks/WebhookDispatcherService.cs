namespace AtcSoft.VideoSurveillance.Api.Webhooks;

/// <summary>
/// Sends the server's camera events to the configured webhooks. The events
/// are raised on the threads that record and analyse video, so they are only
/// queued there; the queue is emptied, and the HTTP calls made, on this
/// worker's own tick. Without configured receivers nothing is queued.
/// </summary>
public sealed partial class WebhookDispatcherService : BackgroundServiceBase<WebhookDispatcherService>
{
    /// <summary>
    /// The most events that wait to be sent. When receivers are slower than
    /// the events arrive, the oldest are dropped rather than memory growing.
    /// </summary>
    public const int QueueCapacity = 500;

    private readonly ConcurrentQueue<WebhookEvent> queue = new();
    private readonly WebhookRegistry registry;
    private readonly ConcurrentDictionary<Guid, byte> camerasInMotion = new();
    private readonly IWebhookSender sender;
    private readonly IRecordingService recordingService;
    private readonly IMotionDetectionService motionDetectionService;
    private readonly ICameraStorageService cameraStorageService;
    private readonly IApplicationSettingsService settingsService;
    private readonly ServerRecordingService? serverRecordingService;

    public WebhookDispatcherService(
        ILogger<WebhookDispatcherService> logger,
        WebhookDispatcherServiceOptions options,
        WebhookRegistry registry,
        IWebhookSender sender,
        IRecordingService recordingService,
        IMotionDetectionService motionDetectionService,
        ICameraStorageService cameraStorageService,
        IApplicationSettingsService settingsService,
        ServerRecordingService? serverRecordingService = null)
        : base(logger, options)
    {
        ArgumentNullException.ThrowIfNull(registry);

        this.registry = registry;
        this.sender = sender;
        this.recordingService = recordingService;
        this.motionDetectionService = motionDetectionService;
        this.cameraStorageService = cameraStorageService;
        this.settingsService = settingsService;
        this.serverRecordingService = serverRecordingService;
    }

    /// <summary>
    /// Gets the number of events waiting to be sent.
    /// </summary>
    public int QueuedCount => queue.Count;

    /// <inheritdoc />
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // Always, also without receivers: one can be added while the server runs.
        recordingService.RecordingStateChanged += OnRecordingStateChanged;
        motionDetectionService.MotionDetected += OnMotionDetected;
        if (serverRecordingService is not null)
        {
            serverRecordingService.CameraConnectionStateChanged += OnCameraConnectionStateChanged;
        }

        LogStarted(registry.ActiveEndpoints.Count);

        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        recordingService.RecordingStateChanged -= OnRecordingStateChanged;
        motionDetectionService.MotionDetected -= OnMotionDetected;
        if (serverRecordingService is not null)
        {
            serverRecordingService.CameraConnectionStateChanged -= OnCameraConnectionStateChanged;
        }

        return base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Queues an event for sending.
    /// </summary>
    /// <param name="webhookEvent">The event.</param>
    /// <returns><c>false</c> when there is nobody to send it to.</returns>
    public bool Enqueue(WebhookEvent webhookEvent)
    {
        ArgumentNullException.ThrowIfNull(webhookEvent);

        if (registry.ActiveEndpoints.Count == 0)
        {
            return false;
        }

        queue.Enqueue(webhookEvent);

        var dropped = 0;
        while (queue.Count > QueueCapacity && queue.TryDequeue(out _))
        {
            dropped++;
        }

        if (dropped > 0)
        {
            LogDropped(dropped);
        }

        return true;
    }

    /// <inheritdoc />
    public override async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && queue.TryDequeue(out var webhookEvent))
        {
            // Asked for each event: a receiver added or changed through the
            // API gets the next one.
            foreach (var endpoint in registry.ActiveEndpoints)
            {
                if (!endpoint.Wants(webhookEvent.Type))
                {
                    continue;
                }

                try
                {
                    await sender.SendAsync(endpoint, webhookEvent, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // One receiver must not keep the event from the others.
                    LogSendFailed(webhookEvent.Type, endpoint.LogName, ex.GetType().Name);
                }
            }
        }
    }

    private void OnRecordingStateChanged(
        object? sender,
        RecordingStateChangedEventArgs e)
    {
        if (WebhookEventFactory.FromRecording(e, GetCameraName(e.CameraId), settingsService.Recording.RecordingPath) is { } webhookEvent)
        {
            Enqueue(webhookEvent);
        }
    }

    // While something moves, the detector reports every picture it moved
    // in (for live overlays). A receiver is told of the motion, once when
    // it starts and once when it is over.
    private void OnMotionDetected(
        object? sender,
        MotionDetectedEventArgs e)
    {
        var changed = e.IsMotionActive
            ? camerasInMotion.TryAdd(e.CameraId, 0)
            : camerasInMotion.TryRemove(e.CameraId, out _);

        if (changed)
        {
            Enqueue(WebhookEventFactory.FromMotion(e, GetCameraName(e.CameraId)));
        }
    }

    private void OnCameraConnectionStateChanged(
        Guid cameraId,
        ConnectionState newState)
    {
        if (WebhookEventFactory.FromConnection(cameraId, GetCameraName(cameraId), newState) is { } webhookEvent)
        {
            Enqueue(webhookEvent);
        }
    }

    private string GetCameraName(Guid cameraId)
        => cameraStorageService.GetCameraById(cameraId)?.Display.DisplayName ?? string.Empty;
}