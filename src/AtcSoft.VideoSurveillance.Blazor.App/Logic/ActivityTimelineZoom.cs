namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// Zooming a recording's activity timeline, from the whole recording down to a minute. What the
/// timeline shows is a window of the recording, its start and end as fractions of the recording,
/// 0 to 1. Events, jumps and seeking stay in fractions of the whole recording; only drawing and
/// clicks go through the window. A copy of Core's <c>ActivityTimelineZoom</c>, kept equal by a test.
/// </summary>
public static class ActivityTimelineZoom
{
    /// <summary>
    /// The shortest window, in seconds: a minute.
    /// </summary>
    public const double ShortestSeconds = 60;

    /// <summary>
    /// The fewest stretches a recording's activity is loaded in.
    /// </summary>
    public const int LeastBuckets = 400;

    /// <summary>
    /// The most stretches a recording's activity is loaded in: the most the server gives.
    /// </summary>
    public const int MostBuckets = 4000;

    /// <summary>
    /// Gets how many stretches to load a recording's activity in: one a second, so a minute zoomed
    /// in still has its seconds, between <see cref="LeastBuckets"/> and <see cref="MostBuckets"/>.
    /// </summary>
    /// <param name="durationSeconds">How long the recording is, in seconds; 0 or less when not known.</param>
    public static int BucketsFor(double durationSeconds)
        => durationSeconds > 0
            ? (int)Math.Clamp(Math.Ceiling(durationSeconds), LeastBuckets, MostBuckets)
            : LeastBuckets;

    /// <summary>
    /// Gets whether the window can get shorter: it is longer than a minute.
    /// </summary>
    /// <param name="start">The start of the window, 0 to 1.</param>
    /// <param name="end">The end of the window, 0 to 1.</param>
    /// <param name="durationSeconds">How long the recording is, in seconds.</param>
    public static bool CanZoomIn(
        double start,
        double end,
        double durationSeconds)
        => end - start > Shortest(durationSeconds) + 1e-9;

    /// <summary>
    /// Gets whether the window can get longer: it is not the whole recording.
    /// </summary>
    /// <param name="start">The start of the window, 0 to 1.</param>
    /// <param name="end">The end of the window, 0 to 1.</param>
    public static bool CanZoomOut(
        double start,
        double end)
        => end - start < 1 - 1e-9;

    /// <summary>
    /// Zooms in to half the window around a place, down to a minute.
    /// </summary>
    /// <param name="start">The start of the window, 0 to 1.</param>
    /// <param name="end">The end of the window, 0 to 1.</param>
    /// <param name="focus">The place in the recording that stays where it is; the middle when outside the window.</param>
    /// <param name="durationSeconds">How long the recording is, in seconds.</param>
    public static (double Start, double End) In(
        double start,
        double end,
        double focus,
        double durationSeconds)
        => Scale(start, end, focus, 0.5, Shortest(durationSeconds));

    /// <summary>
    /// Zooms out to twice the window around a place, up to the whole recording.
    /// </summary>
    /// <param name="start">The start of the window, 0 to 1.</param>
    /// <param name="end">The end of the window, 0 to 1.</param>
    /// <param name="focus">The place in the recording that stays where it is; the middle when outside the window.</param>
    public static (double Start, double End) Out(
        double start,
        double end,
        double focus)
        => Scale(start, end, focus, 2, 0);

    /// <summary>
    /// Keeps the player in view: when it leaves the window it was in, by playing on or by a seek,
    /// the window moves to start where the player is. A window the player was not in stays, so a
    /// look elsewhere is not taken away.
    /// </summary>
    /// <param name="start">The start of the window, 0 to 1.</param>
    /// <param name="end">The end of the window, 0 to 1.</param>
    /// <param name="previous">Where the player was, 0 to 1.</param>
    /// <param name="position">Where the player is, 0 to 1.</param>
    public static (double Start, double End) Follow(
        double start,
        double end,
        double previous,
        double position)
    {
        var wasInside = previous >= start && previous <= end;
        var isInside = position >= start && position <= end;
        if (!wasInside || isInside)
        {
            return (start, end);
        }

        var length = end - start;
        var moved = Math.Clamp(position, 0, 1 - length);
        return (moved, moved + length);
    }

    /// <summary>
    /// Gets the place in the recording at a place on the timeline.
    /// </summary>
    /// <param name="start">The start of the window, 0 to 1.</param>
    /// <param name="end">The end of the window, 0 to 1.</param>
    /// <param name="share">The place on the timeline, 0 (left) to 1 (right).</param>
    /// <returns>The place in the recording, 0 to 1.</returns>
    public static double ToRecording(
        double start,
        double end,
        double share)
        => start + (Math.Clamp(share, 0, 1) * (end - start));

    /// <summary>
    /// Gets the place on the timeline of a place in the recording; outside 0 to 1 when it is not shown.
    /// </summary>
    /// <param name="start">The start of the window, 0 to 1.</param>
    /// <param name="end">The end of the window, 0 to 1.</param>
    /// <param name="position">The place in the recording, 0 to 1.</param>
    public static double ToShare(
        double start,
        double end,
        double position)
        => end > start ? (position - start) / (end - start) : 0;

    /// <summary>
    /// Gets the bars the window shows, at most one per column: where more stretches fall on a column
    /// they become one bar with the most of them, never the average.
    /// </summary>
    /// <param name="buckets">The stretches of the whole recording.</param>
    /// <param name="start">The start of the window, 0 to 1.</param>
    /// <param name="end">The end of the window, 0 to 1.</param>
    /// <param name="columns">How many columns the timeline has, usually its width in pixels.</param>
    /// <returns>The bars, from the left, placed as fractions of the timeline.</returns>
    public static IReadOnlyList<ActivityTimelineBar> Bars(
        IReadOnlyList<RecordingActivityBucket> buckets,
        double start,
        double end,
        int columns)
    {
        ArgumentNullException.ThrowIfNull(buckets);

        var count = buckets.Count;
        if (count == 0 || columns <= 0 || end <= start)
        {
            return [];
        }

        // A hair of slack: 1860 / 3600 * 3600 is a little over 1860, which is not a stretch more.
        var first = Math.Clamp((int)Math.Floor((start * count) + 1e-9), 0, count - 1);
        var last = Math.Clamp((int)Math.Ceiling((end * count) - 1e-9), first + 1, count);
        var group = Math.Max(1, (int)Math.Ceiling((last - first) / (double)columns));

        var bars = new List<ActivityTimelineBar>();
        for (var i = first; i < last; i += group)
        {
            var to = Math.Min(i + group, last);
            var level = 0d;
            var analysed = false;
            for (var j = i; j < to; j++)
            {
                if (buckets[j].IsAnalysed)
                {
                    analysed = true;
                    level = Math.Max(level, buckets[j].Level);
                }
            }

            bars.Add(new ActivityTimelineBar(
                Math.Max(ToShare(start, end, (double)i / count), 0),
                Math.Min(ToShare(start, end, (double)to / count), 1),
                level,
                analysed));
        }

        return bars;
    }

    /// <summary>
    /// Gets the events the window shows, placed as fractions of the timeline and cut at its edges.
    /// </summary>
    /// <param name="events">The events of the whole recording.</param>
    /// <param name="start">The start of the window, 0 to 1.</param>
    /// <param name="end">The end of the window, 0 to 1.</param>
    public static IReadOnlyList<ActivityTimelineEvent> Visible(
        IReadOnlyList<ActivityTimelineEvent> events,
        double start,
        double end)
    {
        ArgumentNullException.ThrowIfNull(events);

        return [.. events
            .Where(e => e.End > start && e.Start < end)
            .Select(e => e with
            {
                Start = Math.Max(ToShare(start, end, e.Start), 0),
                End = Math.Min(ToShare(start, end, e.End), 1),
            })];
    }

    private static double Shortest(double durationSeconds)
        => durationSeconds > ShortestSeconds ? ShortestSeconds / durationSeconds : 1;

    private static (double Start, double End) Scale(
        double start,
        double end,
        double focus,
        double factor,
        double shortest)
    {
        var length = end - start;
        if (length <= 0)
        {
            return (0, 1);
        }

        if (focus < start || focus > end)
        {
            focus = start + (length / 2);
        }

        var scaled = Math.Clamp(length * factor, Math.Min(shortest, 1), 1);

        // The focus keeps its place on the screen.
        var share = (focus - start) / length;
        var moved = Math.Clamp(focus - (scaled * share), 0, 1 - scaled);
        return (moved, moved + scaled);
    }
}