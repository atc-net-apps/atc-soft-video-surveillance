namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// Where things go on a recording's activity timeline: the height of each bar, the bands of the
/// events, and the next or previous event from where the player is. Positions are fractions of
/// the recording, 0 to 1. A copy of Core's <c>ActivityTimelineLayout</c> on the server's
/// <see cref="RecordingActivityBucket"/>, kept equal by a test.
/// </summary>
public static class ActivityTimelineLayout
{
    /// <summary>
    /// The narrowest an event band is drawn, in the units of the timeline's width.
    /// </summary>
    public const double MinimumEventWidth = 3;

    /// <summary>
    /// Moving stretches this close together are one event, in seconds (Core's
    /// <c>ActivitySummarizer.MergeGapSeconds</c>, kept equal by a test).
    /// </summary>
    public const int MergeGapSeconds = 5;

    // Rounding of the recording's length over its stretches, so a gap of exactly the most is joined.
    private const double JoinSlack = 1e-6;

    /// <summary>
    /// Gets the height of a bar, 0 to 1: the square root lifts small changes.
    /// </summary>
    /// <param name="level">The level of the stretch, 0 to 100.</param>
    public static double BarHeight(double level)
        => Math.Sqrt(Math.Clamp(level, 0, 100) / 100);

    /// <summary>
    /// Finds the events: the runs of stretches in which something moved, those
    /// <see cref="MergeGapSeconds"/> or less apart joined: someone stopping for a moment is one visit.
    /// </summary>
    /// <param name="buckets">The stretches of the recording, from its start.</param>
    /// <param name="durationSeconds">How long the recording is, in seconds; 0 or less joins nothing.</param>
    /// <returns>The events, from the start.</returns>
    public static IReadOnlyList<ActivityTimelineEvent> FindEvents(
        IReadOnlyList<RecordingActivityBucket> buckets,
        double durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(buckets);

        var events = new List<ActivityTimelineEvent>();
        var count = buckets.Count;
        var joined = count > 0 && durationSeconds > 0
            ? MergeGapSeconds * count / durationSeconds
            : 0;
        int? start = null;
        var end = 0;
        for (var i = 0; i < count; i++)
        {
            if (buckets[i].Tracks <= 0)
            {
                continue;
            }

            if (start is { } first && i - end > joined + JoinSlack)
            {
                events.Add(new ActivityTimelineEvent((double)first / count, (double)end / count, MainZone(buckets, first, end)));
                start = null;
            }

            start ??= i;
            end = i + 1;
        }

        if (start is { } last)
        {
            events.Add(new ActivityTimelineEvent((double)last / count, (double)end / count, MainZone(buckets, last, end)));
        }

        return events;
    }

    /// <summary>
    /// Gets the watch zone an event happened in: the one in most of its stretches, the one seen
    /// first on a tie; <see langword="null"/> when none was.
    /// </summary>
    /// <param name="buckets">The stretches of the recording.</param>
    /// <param name="from">The event's first stretch.</param>
    /// <param name="to">The stretch after its last.</param>
    public static string? MainZone(
        IReadOnlyList<RecordingActivityBucket> buckets,
        int from,
        int to)
    {
        ArgumentNullException.ThrowIfNull(buckets);

        string? main = null;
        var mainCount = 0;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = Math.Max(from, 0); i < Math.Min(to, buckets.Count); i++)
        {
            foreach (var zone in buckets[i].Zones ?? [])
            {
                var count = counts[zone] = counts.GetValueOrDefault(zone) + 1;
                if (count > mainCount)
                {
                    main = zone;
                    mainCount = count;
                }
            }
        }

        return main;
    }

    /// <summary>
    /// Gets where an event band is drawn on a timeline of the given width: at least
    /// <see cref="MinimumEventWidth"/> wide, widened around its middle, and kept on the timeline.
    /// </summary>
    /// <param name="activityEvent">The event.</param>
    /// <param name="width">The width of the timeline.</param>
    /// <returns>The left edge and the width.</returns>
    public static (double X, double Width) ToPixels(
        ActivityTimelineEvent activityEvent,
        double width)
    {
        ArgumentNullException.ThrowIfNull(activityEvent);

        if (width <= 0)
        {
            return (0, 0);
        }

        var x = activityEvent.Start * width;
        var w = (activityEvent.End - activityEvent.Start) * width;
        if (w < MinimumEventWidth)
        {
            var bandWidth = Math.Min(MinimumEventWidth, width);
            x = Math.Clamp(x + (w / 2) - (bandWidth / 2), 0, width - bandWidth);
            w = bandWidth;
        }

        return (x, w);
    }

    /// <summary>
    /// Gets the start of the first event after the position.
    /// </summary>
    /// <param name="events">The events, from the start.</param>
    /// <param name="position">Where the player is, 0 to 1.</param>
    /// <returns>Where to go, or <see langword="null"/> when no event comes after.</returns>
    public static double? NextEventStart(
        IReadOnlyList<ActivityTimelineEvent> events,
        double position)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var activityEvent in events)
        {
            if (activityEvent.Start > position)
            {
                return activityEvent.Start;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the start of the event before the position; just after an event's start, that event
    /// is skipped, the way a player's "previous" goes one back.
    /// </summary>
    /// <param name="events">The events, from the start.</param>
    /// <param name="position">Where the player is, 0 to 1.</param>
    /// <param name="slack">How far after a start still counts as at it, 0 to 1.</param>
    /// <returns>Where to go, or <see langword="null"/> when no event comes before.</returns>
    public static double? PreviousEventStart(
        IReadOnlyList<ActivityTimelineEvent> events,
        double position,
        double slack)
    {
        ArgumentNullException.ThrowIfNull(events);

        double? previous = null;
        foreach (var activityEvent in events)
        {
            if (activityEvent.Start >= position - slack)
            {
                break;
            }

            previous = activityEvent.Start;
        }

        return previous;
    }

    /// <summary>
    /// Gets where a player that plays the events only should be: inside an event, with a lead-in
    /// before and a lead-out after it, it plays on; before an event it jumps to that event's
    /// lead-in; after the last one it goes to the end.
    /// </summary>
    /// <param name="events">The events, from the start.</param>
    /// <param name="position">Where the player is, 0 to 1.</param>
    /// <param name="leadIn">How much before an event is shown, 0 to 1.</param>
    /// <param name="leadOut">How much after an event is shown, 0 to 1.</param>
    /// <returns>
    /// Where to go, 0 to 1 (1 is the end), or <see langword="null"/> to play on: inside an event,
    /// or for a recording without events, which plays as it is.
    /// </returns>
    public static double? EventsOnlyTarget(
        IReadOnlyList<ActivityTimelineEvent> events,
        double position,
        double leadIn,
        double leadOut)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return null;
        }

        foreach (var activityEvent in events)
        {
            if (position >= activityEvent.Start - leadIn && position <= activityEvent.End + leadOut)
            {
                return null;
            }

            if (activityEvent.Start - leadIn > position)
            {
                return Math.Max(activityEvent.Start - leadIn, 0);
            }
        }

        return 1;
    }
}