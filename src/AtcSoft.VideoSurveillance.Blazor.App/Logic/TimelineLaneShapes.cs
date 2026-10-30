namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The shapes of one camera's lane on the Timeline page, drawn as SVG in a box one unit wide per
/// stretch and <see cref="ActivityTimelineShapes.BoxHeight"/> high: gaps where nothing was
/// recorded, a dim stretch where a recording was not analysed, bars for how much changed, and an
/// orange band where something went somewhere. The same look as the player's timeline.
/// Neighbouring stretches of the same kind are one rectangle, to keep the drawing small.
/// </summary>
public static class TimelineLaneShapes
{
    /// <summary>
    /// Gets the gaps: where no recording covers the span, placed by their time, each with the
    /// colour and the text of its reason.
    /// </summary>
    /// <param name="lane">The camera's lane.</param>
    /// <param name="span">The span the lane covers.</param>
    public static IReadOnlyList<TimelineGapShape> Gaps(
        TimelineLane lane,
        TimelineSpan span)
    {
        ArgumentNullException.ThrowIfNull(lane);
        ArgumentNullException.ThrowIfNull(span);

        var width = lane.Buckets.Count;
        return [.. lane.Gaps
            .Select(g => (Gap: g, Start: span.FractionOf(Clamp(g.Start, span)) ?? 0, End: span.FractionOf(Clamp(g.End, span)) ?? 1))
            .Where(g => g.End > g.Start)
            .Select(g => new TimelineGapShape(
                new TimelineRect(g.Start * width, 0, (g.End - g.Start) * width, ActivityTimelineShapes.BoxHeight),
                TimelineGapStyle.Fill(g.Gap.Reason.Value),
                TimelineGapStyle.Text(g.Gap.Reason.Value)))];
    }

    /// <summary>
    /// Gets the stretches recorded but not analysed: unknown, not quiet.
    /// </summary>
    /// <param name="lane">The camera's lane.</param>
    public static IReadOnlyList<TimelineRect> NotAnalysed(TimelineLane lane)
    {
        ArgumentNullException.ThrowIfNull(lane);

        return Runs(lane.Buckets, b => b.IsRecorded && !b.IsAnalysed, ActivityTimelineShapes.BoxHeight - ActivityTimelineShapes.BandHeight);
    }

    /// <summary>
    /// Gets the bars of the analysed stretches in which something changed.
    /// </summary>
    /// <param name="lane">The camera's lane.</param>
    public static IReadOnlyList<TimelineRect> Bars(TimelineLane lane)
    {
        ArgumentNullException.ThrowIfNull(lane);

        var area = ActivityTimelineShapes.BoxHeight - ActivityTimelineShapes.BandHeight;
        var bars = new List<TimelineRect>();
        for (var i = 0; i < lane.Buckets.Count; i++)
        {
            var height = ActivityTimelineLayout.BarHeight(lane.Buckets[i].Level) * area;
            if (lane.Buckets[i].IsAnalysed && height > 0)
            {
                bars.Add(new TimelineRect(i, area - height, 1, height));
            }
        }

        return bars;
    }

    /// <summary>
    /// Gets the bands of the lane's events, placed by their time to the second and at least
    /// <see cref="ActivityTimelineLayout.MinimumEventWidth"/> stretches wide.
    /// </summary>
    /// <param name="lane">The camera's lane.</param>
    /// <param name="span">The span the lane covers.</param>
    public static IReadOnlyList<TimelineRect> Bands(
        TimelineLane lane,
        TimelineSpan span)
    {
        ArgumentNullException.ThrowIfNull(lane);
        ArgumentNullException.ThrowIfNull(span);

        var width = lane.Buckets.Count;
        return [.. lane.Events
            .Select(e => new ActivityTimelineEvent(span.FractionOf(Clamp(e.Start, span)) ?? 0, span.FractionOf(Clamp(e.End, span)) ?? 1))
            .Select(e => ActivityTimelineLayout.ToPixels(e, width))
            .Select(b => new TimelineRect(b.X, ActivityTimelineShapes.BoxHeight - ActivityTimelineShapes.BandHeight, b.Width, ActivityTimelineShapes.BandHeight))];
    }

    private static DateTimeOffset Clamp(
        DateTimeOffset moment,
        TimelineSpan span)
        => moment < span.From ? span.From : moment > span.To ? span.To : moment;

    private static List<TimelineRect> Runs(
        IReadOnlyList<TimelineBucket> buckets,
        Func<TimelineBucket, bool> matches,
        double height)
    {
        var runs = new List<TimelineRect>();
        int? start = null;
        for (var i = 0; i <= buckets.Count; i++)
        {
            var match = i < buckets.Count && matches(buckets[i]);
            if (match && start is null)
            {
                start = i;
            }
            else if (!match && start is { } first)
            {
                runs.Add(new TimelineRect(first, 0, i - first, height));
                start = null;
            }
        }

        return runs;
    }
}