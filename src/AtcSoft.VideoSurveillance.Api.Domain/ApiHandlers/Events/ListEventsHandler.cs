namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Events;

/// <summary>
/// Handler business logic for the ListEvents operation: the camera events as
/// Server-Sent Events.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Every subscriber reads the one <see cref="SurveillanceEventFeed"/>; a reader
/// that falls behind is cut off instead of holding up the others.</item>
/// <item>Each event's id is its sequence in the feed; a client that sends the last id
/// it read as Last-Event-ID gets the events after it from the replay buffer, and the
/// X-Events-Missed header says how many the buffer no longer had.</item>
/// <item>When nothing happens for a while, a named event <c>ping</c>: the event writer
/// has no comment line, and the generated clients read a named event past. It keeps
/// a proxy that closes an idle connection from closing this one.</item>
/// </list>
/// </remarks>
public sealed class ListEventsHandler(SurveillanceEventFeed feed) : IListEventsHandler
{
    private const string Heartbeat = "ping";
    private const int DefaultHeartbeatSeconds = 15;

    /// <summary>
    /// Executes the ListEvents operation.
    /// </summary>
    public Task<ListEventsResult> ExecuteAsync(
        ListEventsParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        long? lastSequence = long.TryParse(parameters.LastEventId, NumberStyles.None, CultureInfo.InvariantCulture, out var last)
            ? last
            : null;

        // The events own the subscription: they leave the feed when the stream ends.
        var subscription = feed.Subscribe(lastSequence);
        var events = EventsAsync(
            subscription,
            parameters.CameraId,
            TimeSpan.FromSeconds(parameters.HeartbeatSeconds ?? DefaultHeartbeatSeconds),
            cancellationToken);

        return Task.FromResult(ListEventsResult.Ok(events, xEventsMissed: subscription.Missed));
    }

    private static async IAsyncEnumerable<SseItem<SurveillanceEvent>> EventsAsync(
        SurveillanceEventSubscription subscription,
        Guid? cameraId,
        TimeSpan heartbeat,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var owned = subscription;

        foreach (var replayed in subscription.Replayed.Where(e => IsFor(e, cameraId)))
        {
            yield return Event(replayed);
        }

        // The next event, or the heartbeat when it takes longer than the interval: the
        // wait for the event goes on across heartbeats, so none is missed.
        Task<bool>? waiting = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            waiting ??= subscription.Live.WaitToReadAsync(cancellationToken).AsTask();

            bool quiet;
            using (var beat = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                quiet = await Task.WhenAny(waiting, Task.Delay(heartbeat, beat.Token)).ConfigureAwait(false) != waiting;
                await beat.CancelAsync().ConfigureAwait(false);
            }

            if (quiet)
            {
                yield return new SseItem<SurveillanceEvent>(default!, Heartbeat);
                continue;
            }

            // False: the feed ended this subscription; a reader cut off for falling
            // behind gets the feed's exception here, and its connection drops.
            if (!await waiting.ConfigureAwait(false))
            {
                yield break;
            }

            waiting = null;
            while (subscription.Live.TryRead(out var live))
            {
                if (IsFor(live, cameraId))
                {
                    yield return Event(live);
                }
            }
        }
    }

    private static bool IsFor(
        SequencedEvent sequenced,
        Guid? cameraId)
        => cameraId is null || sequenced.Event.CameraId == cameraId;

    private static SseItem<SurveillanceEvent> Event(SequencedEvent sequenced)
        => new(sequenced.Event)
        {
            EventId = sequenced.Sequence.ToString(CultureInfo.InvariantCulture),
        };
}