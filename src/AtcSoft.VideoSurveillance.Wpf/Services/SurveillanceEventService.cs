namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// Reads the server's camera events (GET /events) for as long as the app runs,
/// and raises each part of an event as its own event. When the stream drops, it
/// asks again with the id of the last event it read (Last-Event-ID), so the
/// server sends what came meanwhile; while the server cannot be reached, it
/// waits a little longer each time.
/// </summary>
/// <remarks>
/// The events are raised on the thread that reads the stream, not the UI thread.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1003:Use generic event handler instances",
    Justification = "Action<T> events are intentional for terse WPF consumption.")]
[SuppressMessage(
    "Design",
    "MA0046:Use EventHandler<T> to declare events",
    Justification = "Action<T> events are intentional for terse WPF consumption.")]
public sealed class SurveillanceEventService : BackgroundServiceBase<SurveillanceEventService>
{
    private readonly IListEventsEndpoint endpoint;
    private readonly ClientSession? session;

    public SurveillanceEventService(
        IListEventsEndpoint endpoint,
        ILogger<SurveillanceEventService> logger,
        SurveillanceEventServiceOptions options,
        ClientSession? session = null)
        : base(logger, options)
    {
        this.endpoint = endpoint;
        this.session = session;
    }

    public event Action<ConnectionStateChangedEvent>? OnConnectionStateChanged;

    public event Action<RecordingStateChangedEvent>? OnRecordingStateChanged;

    public event Action<MotionDetectedEvent>? OnMotionDetected;

    public event Action<UsbCameraLifecycleEvent>? OnUsbCameraLifecycleChanged;

    /// <summary>
    /// Raised when activity on a recorded camera starts or ends: an open timeline grows by it.
    /// </summary>
    public event Action<ActivityChangedEvent>? OnActivityChanged;

    /// <summary>
    /// Raised when the stream opens and the client may have missed events:
    /// the server was not there at the start (what the client showed came from
    /// this computer alone), or it could not send again all that came while
    /// the stream was down (it restarted). Whoever shows the server's state
    /// loads it again.
    /// </summary>
    public event Action? OnMissedEvents;

    /// <summary>
    /// Raised when <see cref="IsReading"/> changes.
    /// </summary>
    public event Action? ReadingChanged;

    /// <summary>
    /// Gets a value indicating whether the stream is open now.
    /// </summary>
    public bool IsReading { get; private set; }

    /// <summary>
    /// Raised when <see cref="IsConnected"/> changes.
    /// </summary>
    public event Action? ConnectedChanged;

    /// <summary>
    /// Gets a value indicating whether the server answered the last time it
    /// was asked. A stream that drops and is opened again at once does not
    /// change it; a server that cannot be reached does.
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// Gets or sets the clock the waits between connections run on (tests replace it).
    /// </summary>
    public TimeProvider Time { get; set; } = TimeProvider.System;

    /// <inheritdoc />
    public override Task DoWorkAsync(CancellationToken stoppingToken)
        => RunAsync(stoppingToken);

    /// <summary>
    /// Reads the events until <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        // The server answers a client that is not signed in with 401.
        if (session is not null)
        {
            try
            {
                await session.WaitUntilSignedInAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        var opened = false;
        var failedBeforeOpening = false;
        var policy = new ServerSentEventReconnectPolicy
        {
            TimeProvider = Time,
            OnConnected = result =>
            {
                // The first stream may have missed what came while the server was away;
                // a later one, what the server could not send again (it restarted).
                var missed = opened
                    ? (result as IListEventsEndpointResult)?.XEventsMissedHeader is > 0
                    : failedBeforeOpening;
                opened = true;
                SetConnected(connected: true);
                SetReading(true);
                if (missed)
                {
                    OnMissedEvents?.Invoke();
                }
            },
            OnDisconnected = (exception, status) =>
            {
                if (IsReading)
                {
                    SetReading(false);
                }

                // A stream the server ended (or the caller left) keeps the server connected.
                if (exception is not null || status is not null)
                {
                    failedBeforeOpening |= !opened;
                    SetConnected(connected: false);
                }
            },
        };

        try
        {
            await foreach (var surveillanceEvent in endpoint
                .ReadResumingAsync(new ListEventsParameters(CameraId: null, HeartbeatSeconds: null, LastEventId: null), policy, cancellationToken: cancellationToken)
                .ConfigureAwait(false))
            {
                if (surveillanceEvent is not null)
                {
                    Raise(surveillanceEvent);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The app stops.
        }
    }

    // The part that is filled says what happened; an event of a type this
    // client does not know has none of them and is read past.
    private void Raise(SurveillanceEvent surveillanceEvent)
    {
        if (surveillanceEvent.Connection is { } connection)
        {
            OnConnectionStateChanged?.Invoke(connection);
        }

        if (surveillanceEvent.Recording is { } recording)
        {
            OnRecordingStateChanged?.Invoke(recording);
        }

        if (surveillanceEvent.Motion is { } motion)
        {
            OnMotionDetected?.Invoke(motion);
        }

        if (surveillanceEvent.Usb is { } usb)
        {
            OnUsbCameraLifecycleChanged?.Invoke(usb);
        }

        if (surveillanceEvent.Activity is { } activity)
        {
            OnActivityChanged?.Invoke(activity);
        }
    }

    private void SetConnected(bool connected)
    {
        if (IsConnected == connected)
        {
            return;
        }

        IsConnected = connected;
        ConnectedChanged?.Invoke();
    }

    private void SetReading(bool reading)
    {
        IsReading = reading;
        ReadingChanged?.Invoke();
    }
}