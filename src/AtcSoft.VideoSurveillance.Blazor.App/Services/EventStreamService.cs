namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// Reads the server's camera events (GET /events) for as long as it runs,
/// through the generated resuming reader.
/// </summary>
public sealed class EventStreamService(IListEventsEndpoint endpoint)
{
    /// <summary>
    /// Raised when <see cref="IsReading"/> changes, so a page can show it.
    /// </summary>
    public event Action? ReadingChanged;

    /// <summary>
    /// Gets a value indicating whether the stream is open now.
    /// </summary>
    public bool IsReading { get; private set; }

    /// <summary>
    /// Gets or sets the clock the waits between connections run on (tests replace it).
    /// </summary>
    public TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>
    /// Reads the events until <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    public async Task RunAsync(
        Func<SurveillanceEvent, Task> onEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onEvent);

        var policy = new ServerSentEventReconnectPolicy
        {
            TimeProvider = Time,
            OnConnected = _ => SetReading(true),
            OnDisconnected = (_, _) => SetReading(false),
        };

        try
        {
            await foreach (var surveillanceEvent in endpoint
                .ReadResumingAsync(new ListEventsParameters(CameraId: null, HeartbeatSeconds: null, LastEventId: null), policy, cancellationToken: cancellationToken)
                .ConfigureAwait(false))
            {
                if (surveillanceEvent is not null)
                {
                    await onEvent(surveillanceEvent).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The page was left.
        }
    }

    private void SetReading(bool reading)
    {
        IsReading = reading;
        ReadingChanged?.Invoke();
    }
}