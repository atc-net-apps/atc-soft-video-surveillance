namespace AtcSoft.VideoSurveillance.Api.Domain.Events;

/// <summary>
/// One subscriber's place in the <see cref="SurveillanceEventFeed"/>: what the replay
/// buffer had after its last event, how many it no longer had, and the live events.
/// Disposing it leaves the feed.
/// </summary>
public sealed class SurveillanceEventSubscription : IDisposable
{
    private readonly Action leave;

    public SurveillanceEventSubscription(
        IReadOnlyList<SequencedEvent> replayed,
        long missed,
        ChannelReader<SequencedEvent> live,
        Action leave)
    {
        Replayed = replayed;
        Missed = missed;
        Live = live;
        this.leave = leave;
    }

    public IReadOnlyList<SequencedEvent> Replayed { get; }

    public long Missed { get; }

    public ChannelReader<SequencedEvent> Live { get; }

    public void Dispose()
        => leave();
}