namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The shapes of a recording's activity timeline under the web player, drawn as SVG in a box
/// <see cref="BoxWidth"/> wide and <see cref="BoxHeight"/> high, stretched to the player's width:
/// bars for how much changed, a band at the bottom where something went somewhere, and dim stretches
/// that were not analysed. The box shows the part of the recording from <c>start</c> to <c>end</c>
/// (fractions of the recording, see <see cref="ActivityTimelineZoom"/>). The same look as the
/// desktop apps' timeline.
/// </summary>
public static class ActivityTimelineShapes
{
    /// <summary>
    /// The width of the drawing's box: its columns, at most one bar each.
    /// </summary>
    public const int BoxWidth = 400;

    /// <summary>
    /// The height of the drawing's box.
    /// </summary>
    public const double BoxHeight = 100;

    /// <summary>
    /// The height of an event band at the bottom of the box.
    /// </summary>
    public const double BandHeight = 16;

    /// <summary>
    /// Gets the bars of the analysed stretches in which something changed.
    /// </summary>
    /// <param name="buckets">The stretches of the recording.</param>
    /// <param name="start">Where the part shown starts, 0 to 1.</param>
    /// <param name="end">Where the part shown ends, 0 to 1.</param>
    public static IReadOnlyList<TimelineRect> Bars(
        IReadOnlyList<RecordingActivityBucket> buckets,
        double start = 0,
        double end = 1)
    {
        var area = BoxHeight - BandHeight;
        return [.. ActivityTimelineZoom.Bars(buckets, start, end, BoxWidth)
            .Where(b => b.IsAnalysed)
            .Select(b => (Bar: b, Height: ActivityTimelineLayout.BarHeight(b.Level) * area))
            .Where(b => b.Height > 0)
            .Select(b => new TimelineRect(b.Bar.Start * BoxWidth, area - b.Height, (b.Bar.End - b.Bar.Start) * BoxWidth, b.Height))];
    }

    /// <summary>
    /// Gets the stretches that were not analysed, which are unknown rather than quiet.
    /// </summary>
    /// <param name="buckets">The stretches of the recording.</param>
    /// <param name="start">Where the part shown starts, 0 to 1.</param>
    /// <param name="end">Where the part shown ends, 0 to 1.</param>
    public static IReadOnlyList<TimelineRect> NotAnalysed(
        IReadOnlyList<RecordingActivityBucket> buckets,
        double start = 0,
        double end = 1)
        => [.. ActivityTimelineZoom.Bars(buckets, start, end, BoxWidth)
            .Where(b => !b.IsAnalysed)
            .Select(b => new TimelineRect(b.Start * BoxWidth, 0, (b.End - b.Start) * BoxWidth, BoxHeight - BandHeight))];

    /// <summary>
    /// Gets the bands of the events with their colours, at least
    /// <see cref="ActivityTimelineLayout.MinimumEventWidth"/> columns wide: an event in a watch zone
    /// with a colour in that colour, else orange.
    /// </summary>
    /// <param name="events">The events of the recording (<see cref="ActivityTimelineLayout.FindEvents"/>).</param>
    /// <param name="zoneColours">The colours of the camera's watch zones (<see cref="MotionZoneColors.ByZone"/>).</param>
    /// <param name="start">Where the part shown starts, 0 to 1.</param>
    /// <param name="end">Where the part shown ends, 0 to 1.</param>
    public static IReadOnlyList<(TimelineRect Band, string Fill)> Bands(
        IReadOnlyList<ActivityTimelineEvent> events,
        IReadOnlyDictionary<string, string>? zoneColours,
        double start = 0,
        double end = 1)
        => [.. ActivityTimelineZoom.Visible(events, start, end)
            .Select(e => (Pixels: ActivityTimelineLayout.ToPixels(e, BoxWidth), e.Zone))
            .Select(b => (new TimelineRect(b.Pixels.X, BoxHeight - BandHeight, b.Pixels.Width, BandHeight), MotionZoneColors.ForEvent(b.Zone, zoneColours)))];

    /// <summary>
    /// Gets the playhead: a line the full height of the box where the player is, kept inside the box.
    /// </summary>
    /// <param name="position">Where the player is, 0 to 1; below 0 before it plays.</param>
    /// <param name="start">Where the part shown starts, 0 to 1.</param>
    /// <param name="end">Where the part shown ends, 0 to 1.</param>
    /// <returns>The line, or <see langword="null"/> while there is no position or it is not in the part shown.</returns>
    public static TimelineRect? Playhead(
        double position,
        double start = 0,
        double end = 1)
    {
        var share = ActivityTimelineZoom.ToShare(start, end, Math.Min(position, 1));
        if (position < 0 || share is < 0 or > 1)
        {
            return null;
        }

        const double width = BoxWidth / 200.0;
        return new TimelineRect(Math.Clamp((share * BoxWidth) - (width / 2), 0, BoxWidth - width), 0, width, BoxHeight);
    }

    /// <summary>
    /// Gets the column the playhead is in, so the timeline is drawn again only when it moves to another.
    /// </summary>
    /// <param name="position">Where the player is, 0 to 1; below 0 before it plays.</param>
    /// <param name="start">Where the part shown starts, 0 to 1.</param>
    /// <param name="end">Where the part shown ends, 0 to 1.</param>
    /// <returns>The column; -1 while it is not shown.</returns>
    public static int PlayheadColumn(
        double position,
        double start = 0,
        double end = 1)
        => Playhead(position, start, end) is { } line ? (int)line.X : -1;
}