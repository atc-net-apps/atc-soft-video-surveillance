namespace AtcSoft.Video.Surveillance.Blazor.App.Services;

/// <summary>
/// Reads the server's camera events (GET /events) for as long as it runs,
/// through the generated resuming reader. A reading the server ends (a 401
/// while the session could not be renewed) starts again after the last event,
/// and an event a page fails on is logged and read past.
/// </summary>
public sealed partial class EventStreamService(
    IListEventsEndpoint endpoint,
    ILogger<EventStreamService>? logger = null)
{
    private readonly ILogger logger = (ILogger?)logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    // Kept across readings, so one that starts again goes on after the last event.
    private string? lastEventId;
    private bool opened;
    private bool failedBeforeOpening;

    /// <summary>
    /// Raised when <see cref="IsReading"/> changes, so a page can show it.
    /// </summary>
    public event Action? ReadingChanged;

    /// <summary>
    /// Raised when the stream opens and the page may have missed events: the
    /// server was not there at the start, it could not send again all that
    /// came while the stream was down (it restarted), or there was no event to
    /// go on after. The page loads what it shows again.
    /// </summary>
    public event Action? MissedEvents;

    /// <summary>
    /// Gets a value indicating whether the stream is open now.
    /// </summary>
    public bool IsReading { get; private set; }

    /// <summary>
    /// Gets or sets the clock the waits between connections run on (tests replace it).
    /// </summary>
    public TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>
    /// Gets the wait before a reading the server ended starts again: long
    /// enough for the session to be renewed after a 401.
    /// </summary>
    public static TimeSpan RestartDelay { get; } = BrowserSession.UnavailableBackOff;

    /// <summary>
    /// Reads the events until <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The reading must go on for as long as the page is open; a failure is logged and the reading starts again.")]
    public async Task RunAsync(
        Func<SurveillanceEvent, Task> onEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onEvent);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ReadAsync(onEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The page was left.
                return;
            }
            catch (Exception ex)
            {
                LogReadingFailed(ex);
            }

            // The server ended the reading (a 401 after a sleep: the token ran out
            // and could not be renewed yet). It starts again after the last event.
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

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A page that fails on one event must not end its event stream.")]
    private async Task ReadAsync(
        Func<SurveillanceEvent, Task> onEvent,
        CancellationToken cancellationToken)
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
                new ListEventsParameters(CameraId: null, HeartbeatSeconds: EventStreamWatchdog.HeartbeatSeconds, LastEventId: lastEventId),
                policy,
                streamReadOptions: readOptions,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false))
        {
            if (surveillanceEvent is null)
            {
                continue;
            }

            try
            {
                await onEvent(surveillanceEvent).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogEventFailed(ex, surveillanceEvent.Type.ToString());
            }
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A page that fails to reload must not end its event stream.")]
    private void OnConnected(IServerSentEventResult result)
    {
        var missed = opened
            ? lastEventId is null || (result as IListEventsEndpointResult)?.XEventsMissedHeader is > 0
            : failedBeforeOpening;
        opened = true;
        SetReading(true);
        if (!missed)
        {
            return;
        }

        try
        {
            MissedEvents?.Invoke();
        }
        catch (Exception ex)
        {
            LogEventFailed(ex, nameof(MissedEvents));
        }
    }

    private void OnDisconnected(
        Exception? exception,
        System.Net.HttpStatusCode? status)
    {
        SetReading(false);
        if (exception is not null || status is not null)
        {
            failedBeforeOpening |= !opened;
        }
    }

    private void SetReading(bool reading)
    {
        IsReading = reading;
        ReadingChanged?.Invoke();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reading the event stream failed; it starts again")]
    private partial void LogReadingFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The page failed on a {Event} event; the event stream goes on")]
    private partial void LogEventFailed(Exception ex, string @event);
}