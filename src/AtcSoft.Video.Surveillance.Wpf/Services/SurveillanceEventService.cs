namespace AtcSoft.Video.Surveillance.Wpf.Services;

/// <summary>
/// Reads the server's camera events (GET /events) for as long as the app runs,
/// and raises each part of an event as its own event. When the stream drops, it
/// asks again with the id of the last event it read (Last-Event-ID), so the
/// server sends what came meanwhile; while the server cannot be reached, it
/// waits a little longer each time. A reading the server ends (a 401 while the
/// session could not be renewed) starts again after the last event too.
/// </summary>
/// <remarks>
/// The events are raised on the thread that reads the stream, not the UI thread.
/// A handler that throws is logged; the others still get the event.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1003:Use generic event handler instances",
    Justification = "Action<T> events are intentional for terse WPF consumption.")]
[SuppressMessage(
    "Design",
    "MA0046:Use EventHandler<T> to declare events",
    Justification = "Action<T> events are intentional for terse WPF consumption.")]
public sealed partial class SurveillanceEventService : BackgroundServiceBase<SurveillanceEventService>
{
    private readonly IListEventsEndpoint endpoint;
    private readonly ClientSession? session;

    // Kept across readings, so one that starts again goes on after the last event.
    private string? lastEventId;
    private bool opened;
    private bool failedBeforeOpening;

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
    /// this computer alone), it could not send again all that came while
    /// the stream was down (it restarted), or there was no event to go on
    /// after. Whoever shows the server's state loads it again.
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

    /// <summary>
    /// Gets the wait before a reading the server ended starts again: long
    /// enough for the session to be renewed after a 401.
    /// </summary>
    public static TimeSpan RestartDelay { get; } = ClientSession.UnavailableBackOff;

    /// <inheritdoc />
    public override Task DoWorkAsync(CancellationToken stoppingToken)
        => RunAsync(stoppingToken);

    /// <summary>
    /// Reads the events until <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The reading must go on for as long as the app runs; a failure is logged and the reading starts again.")]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
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

            try
            {
                await ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The app stops.
                return;
            }
            catch (Exception ex)
            {
                LogReadingFailed(ex);
                SetConnected(connected: false);
            }

            // The server ended the reading (a 401 after a sleep: the token ran out
            // and could not be renewed yet). It starts again after the last event.
            LogReadingEnded(lastEventId);
            try
            {
                await Task.Delay(RestartDelay, Time, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ReadAsync(CancellationToken cancellationToken)
    {
        var policy = new ServerSentEventReconnectPolicy
        {
            TimeProvider = Time,
            OnConnected = OnConnected,
            OnDisconnected = OnDisconnected,
        };
        var readOptions = new StreamReadOptions
        {
            OnEvent = info => lastEventId = info.LastEventId ?? lastEventId,
        };

        await foreach (var surveillanceEvent in endpoint
            .ReadResumingAsync(
                new ListEventsParameters(CameraId: null, HeartbeatSeconds: null, LastEventId: lastEventId),
                policy,
                streamReadOptions: readOptions,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false))
        {
            if (surveillanceEvent is not null)
            {
                Raise(surveillanceEvent);
            }
        }
    }

    private void OnConnected(IServerSentEventResult result)
    {
        // The first stream may have missed what came while the server was away;
        // a later one, what the server could not send again (it restarted), or
        // all that came meanwhile when there was no event to go on after.
        var missed = opened
            ? lastEventId is null || (result as IListEventsEndpointResult)?.XEventsMissedHeader is > 0
            : failedBeforeOpening;
        opened = true;
        SetConnected(connected: true);
        SetReading(true);
        if (missed)
        {
            Invoke(OnMissedEvents, nameof(OnMissedEvents));
        }
    }

    private void OnDisconnected(
        Exception? exception,
        System.Net.HttpStatusCode? status)
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
    }

    // The part that is filled says what happened; an event of a type this
    // client does not know has none of them and is read past.
    private void Raise(SurveillanceEvent surveillanceEvent)
    {
        if (surveillanceEvent.Connection is { } connection)
        {
            Invoke(OnConnectionStateChanged, connection);
        }

        if (surveillanceEvent.Recording is { } recording)
        {
            Invoke(OnRecordingStateChanged, recording);
        }

        if (surveillanceEvent.Motion is { } motion)
        {
            Invoke(OnMotionDetected, motion);
        }

        if (surveillanceEvent.Usb is { } usb)
        {
            Invoke(OnUsbCameraLifecycleChanged, usb);
        }

        if (surveillanceEvent.Activity is { } activity)
        {
            Invoke(OnActivityChanged, activity);
        }
    }

    // Each handler on its own: one that throws neither ends the reading nor
    // keeps the event from the others.
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A view's handler must not end the event stream of every view.")]
    private void Invoke<T>(
        Action<T>? handlers,
        T value)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Action<T>>())
        {
            try
            {
                handler(value);
            }
            catch (Exception ex)
            {
                LogHandlerFailed(ex, typeof(T).Name);
            }
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A view's handler must not end the event stream of every view.")]
    private void Invoke(
        Action? handlers,
        string name)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Action>())
        {
            try
            {
                handler();
            }
            catch (Exception ex)
            {
                LogHandlerFailed(ex, name);
            }
        }
    }

    private void SetConnected(bool connected)
    {
        if (IsConnected == connected)
        {
            return;
        }

        IsConnected = connected;
        Invoke(ConnectedChanged, nameof(ConnectedChanged));
    }

    private void SetReading(bool reading)
    {
        IsReading = reading;
        Invoke(ReadingChanged, nameof(ReadingChanged));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A handler of {Event} failed; the other handlers and the event stream go on")]
    private partial void LogHandlerFailed(Exception ex, string @event);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading the event stream failed; it starts again")]
    private partial void LogReadingFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "The server ended the event stream; it starts again after event {LastEventId}")]
    private partial void LogReadingEnded(string? lastEventId);
}