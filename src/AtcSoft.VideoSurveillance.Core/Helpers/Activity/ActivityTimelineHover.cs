namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// What a recording's activity timeline shows when the mouse rests on it: the most that happened
/// in the stretches under the mouse, and the watch zone of an event there. The web client has a
/// copy under <c>Logic/</c>, kept equal by a test.
/// </summary>
public static class ActivityTimelineHover
{
    /// <summary>
    /// Gets what happened between two places of the recording: the stretches under the mouse,
    /// usually as wide as the narrowest event band, so a widened band tells its event.
    /// </summary>
    /// <param name="buckets">The stretches of the whole recording.</param>
    /// <param name="events">The events of the whole recording (<see cref="ActivityTimelineLayout.FindEvents(IReadOnlyList{ActivityBucket}, double)"/>).</param>
    /// <param name="from">Where the part under the mouse starts, 0 to 1.</param>
    /// <param name="to">Where it ends, 0 to 1.</param>
    /// <returns>What happened there, or <see langword="null"/> without stretches.</returns>
    public static ActivityHover? At(
        IReadOnlyList<ActivityBucket> buckets,
        IReadOnlyList<ActivityEvent> events,
        double from,
        double to)
    {
        ArgumentNullException.ThrowIfNull(buckets);
        ArgumentNullException.ThrowIfNull(events);

        var count = buckets.Count;
        if (count == 0)
        {
            return null;
        }

        (from, to) = (Math.Clamp(Math.Min(from, to), 0, 1), Math.Clamp(Math.Max(from, to), 0, 1));
        var first = Math.Clamp((int)Math.Floor(from * count), 0, count - 1);
        var last = Math.Clamp((int)Math.Ceiling(to * count), first + 1, count);

        var tracks = 0;
        var level = 0d;
        var analysed = false;
        string? bucketZone = null;
        for (var i = first; i < last; i++)
        {
            var bucket = buckets[i];
            if (!bucket.IsAnalysed)
            {
                continue;
            }

            analysed = true;
            tracks = Math.Max(tracks, bucket.Tracks);
            level = Math.Max(level, bucket.Level);
            bucketZone ??= bucket.Zones.Count > 0 ? bucket.Zones[0] : null;
        }

        var eventZone = events.FirstOrDefault(e => e.Zone is not null && e.End > from && e.Start < to)?.Zone;
        return new ActivityHover((from + to) / 2, tracks, level, analysed, eventZone ?? bucketZone);
    }
}