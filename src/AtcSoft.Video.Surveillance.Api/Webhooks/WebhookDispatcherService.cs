namespace AtcSoft.Video.Surveillance.Api.Webhooks;

/// <summary>
/// Sends the server's camera events to the configured webhooks. The events
/// are raised on the threads that record and analyse video, so they are only
/// queued there; the HTTP calls are made from this worker. Without configured
/// receivers nothing is queued.
/// <para>
/// Each receiver has a queue and a sender of its own, so one that is slow or
/// down (each event tried three times, ten seconds each) never holds up the
/// others. A receiver that failed <see cref="FailuresBeforePause"/> events in a
/// row is left alone for <see cref="PauseAfterFailures"/>; its events wait in
/// its queue meanwhile, the oldest dropped when it is full.
/// </para>
/// </summary>
public sealed partial class WebhookDispatcherService : BackgroundServiceBase<WebhookDispatcherService>
{
    /// <summary>
    /// The most events that wait to be sent. When receivers are slower than
    /// the events arrive, the oldest are dropped rather than memory growing.
    /// </summary>
    public const int QueueCapacity = 500;

    /// <summary>
    /// How many events in a row a receiver may fail before it is left alone for a while.
    /// </summary>
    public const int FailuresBeforePause = 3;

    /// <summary>
    /// How long a receiver that keeps failing is left alone.
    /// </summary>
    public static readonly TimeSpan PauseAfterFailures = TimeSpan.FromMinutes(1);

    // How long a tick waits for the receivers' senders before it returns; a
    // slow one goes on in the background and is not started twice.
    private static readonly TimeSpan TickWait = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<string, Lane> lanes = new(StringComparer.Ordinal);
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
    public int QueuedCount => lanes.Values.Sum(l => l.Queue.Count);

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

        var endpoints = registry.ActiveEndpoints;
        if (endpoints.Count == 0)
        {
            return false;
        }

        foreach (var endpoint in endpoints.Where(e => e.Wants(webhookEvent.Type)).DistinctBy(LaneKey, StringComparer.Ordinal))
        {
            var lane = lanes.GetOrAdd(LaneKey(endpoint), _ => new Lane());
            lane.Queue.Enqueue(webhookEvent);

            var dropped = 0;
            while (lane.Queue.Count > QueueCapacity && lane.Queue.TryDequeue(out _))
            {
                dropped++;
            }

            if (dropped > 0)
            {
                LogDropped(dropped, endpoint.LogName);
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        // Asked at every tick: a receiver changed or removed through the API
        // is sent to as it is now, and one that is gone loses its queue.
        // Two receivers alike in every way (address, secret, events) share a queue.
        var endpoints = registry.ActiveEndpoints
            .DistinctBy(LaneKey, StringComparer.Ordinal)
            .ToDictionary(LaneKey, StringComparer.Ordinal);
        foreach (var key in lanes.Keys)
        {
            if (!endpoints.ContainsKey(key))
            {
                lanes.TryRemove(key, out _);
            }
        }

        var running = new List<Task>();
        foreach (var (key, lane) in lanes)
        {
            if (lane.Queue.IsEmpty || !endpoints.TryGetValue(key, out var endpoint))
            {
                continue;
            }

            lock (lane)
            {
                if (lane.Sending is { IsCompleted: false } busy)
                {
                    running.Add(busy);
                    continue;
                }

                lane.Sending = Task.Run(() => SendQueuedAsync(lane, endpoint, stoppingToken), CancellationToken.None);
                running.Add(lane.Sending);
            }
        }

        if (running.Count > 0)
        {
            await Task.WhenAny(Task.WhenAll(running), Task.Delay(TickWait, stoppingToken)).ConfigureAwait(false);
        }
    }

    // Sends one receiver's events in order, until its queue is empty or it
    // keeps failing.
    private async Task SendQueuedAsync(
        Lane lane,
        WebhookEndpoint endpoint,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (DateTimeOffset.UtcNow < lane.PausedUntil || !lane.Queue.TryDequeue(out var webhookEvent))
            {
                return;
            }

            bool delivered;
            try
            {
                delivered = await sender.SendAsync(endpoint, webhookEvent, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogSendFailed(webhookEvent.Type, endpoint.LogName, ex.GetType().Name);
                delivered = false;
            }

            if (delivered)
            {
                lane.Failures = 0;
                continue;
            }

            lane.Failures++;
            if (lane.Failures >= FailuresBeforePause)
            {
                lane.PausedUntil = DateTimeOffset.UtcNow + PauseAfterFailures;
                lane.Failures = 0;
                LogReceiverPaused(endpoint.LogName, FailuresBeforePause, (int)PauseAfterFailures.TotalSeconds);
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

    // A receiver is told from another by everything that makes it one: two
    // with the same address but other secrets or events are two receivers.
    private static string LaneKey(WebhookEndpoint endpoint)
        => string.Join('', endpoint.Url, endpoint.Secret ?? string.Empty, string.Join(',', endpoint.Events));

    private string GetCameraName(Guid cameraId)
        => cameraStorageService.GetCameraById(cameraId)?.Display.DisplayName ?? string.Empty;

    // One receiver's queue, and how it fares.
    private sealed class Lane
    {
        public ConcurrentQueue<WebhookEvent> Queue { get; } = new();

        public Task? Sending { get; set; }

        public int Failures { get; set; }

        public DateTimeOffset PausedUntil { get; set; }
    }
}