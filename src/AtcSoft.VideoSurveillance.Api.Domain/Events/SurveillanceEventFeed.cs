namespace AtcSoft.VideoSurveillance.Api.Domain.Events;

/// <summary>
/// The one feed of camera events every GET /events subscriber shares: the last
/// <see cref="ReplayCapacity"/> events kept for a client that resumes, and a bounded
/// queue per subscriber, so a reader that falls behind is cut off - it resumes after
/// its last event, from the replay buffer - instead of holding up the feed and every
/// other reader.
/// </summary>
/// <remarks>
/// The server's broadcaster publishes the same events it sends over the hub.
/// </remarks>
public sealed class SurveillanceEventFeed
{
    /// <summary>
    /// How many of the latest events a resuming client can get again.
    /// </summary>
    public const int ReplayCapacity = 256;

    /// <summary>
    /// How many events a subscriber may be behind before it is cut off.
    /// </summary>
    public const int SubscriberCapacity = 256;

    private readonly Lock gate = new();
    private readonly Queue<SequencedEvent> replay = new();
    private readonly List<ChannelWriter<SequencedEvent>> subscribers = [];

    // The ids start from the time the feed was made, so those of an earlier
    // run of the server are all lower than the first of this one.
    private readonly long firstSequence;
    private long sequence;

    public SurveillanceEventFeed()
        : this(TimeProvider.System)
    {
    }

    public SurveillanceEventFeed(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        firstSequence = (time.GetUtcNow().ToUnixTimeMilliseconds() * 1000) + 1;
        sequence = firstSequence - 1;
    }

    /// <summary>
    /// Subscribes to the feed: what the replay buffer has after <paramref name="lastSequence"/>,
    /// how many events after it it no longer has, and every event from then on.
    /// An id this run of the feed did not give (the server restarted) gets everything
    /// the buffer has, and at least one missed: what happened meanwhile is not known.
    /// </summary>
    /// <param name="lastSequence">The sequence of the last event the client read, or <see langword="null"/> for a new client.</param>
    public SurveillanceEventSubscription Subscribe(long? lastSequence)
    {
        var channel = Channel.CreateBounded<SequencedEvent>(new BoundedChannelOptions(SubscriberCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
        });

        // Under the lock that publishes: the replay ends where the live events start, nothing twice, nothing lost.
        lock (gate)
        {
            var oldest = replay.Count > 0 ? replay.Peek().Sequence : sequence + 1;
            List<SequencedEvent> replayed;
            long missed;
            if (lastSequence is not { } last)
            {
                replayed = [];
                missed = 0;
            }
            else if (last < firstSequence - 1 || last > sequence)
            {
                replayed = [.. replay];
                missed = Math.Max(1, oldest - firstSequence);
            }
            else
            {
                replayed = [.. replay.Where(e => e.Sequence > last)];
                missed = Math.Max(0, oldest - 1 - last);
            }

            subscribers.Add(channel.Writer);
            return new SurveillanceEventSubscription(replayed, missed, channel.Reader, () => Unsubscribe(channel.Writer));
        }
    }

    /// <summary>
    /// Publishes an event to every subscriber.
    /// </summary>
    /// <returns>The event's sequence, its id on the stream.</returns>
    public long Publish(SurveillanceEvent surveillanceEvent)
    {
        ArgumentNullException.ThrowIfNull(surveillanceEvent);

        lock (gate)
        {
            var sequenced = new SequencedEvent(++sequence, surveillanceEvent);
            replay.Enqueue(sequenced);
            if (replay.Count > ReplayCapacity)
            {
                replay.Dequeue();
            }

            for (var i = subscribers.Count - 1; i >= 0; i--)
            {
                if (subscribers[i].TryWrite(sequenced))
                {
                    continue;
                }

                // A whole queue behind: the reader's stream ends as a dropped connection
                // does, and it resumes after its last event - the feed never waits for it.
                subscribers[i].TryComplete(new IOException($"The subscriber fell {SubscriberCapacity} events behind the feed."));
                subscribers.RemoveAt(i);
            }

            return sequenced.Sequence;
        }
    }

    private void Unsubscribe(ChannelWriter<SequencedEvent> subscriber)
    {
        lock (gate)
        {
            if (subscribers.Remove(subscriber))
            {
                subscriber.TryComplete();
            }
        }
    }
}