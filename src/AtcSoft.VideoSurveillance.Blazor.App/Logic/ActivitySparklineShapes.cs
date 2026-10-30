namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// A recording's sparkline in the Recordings list, drawn as SVG in a box one unit wide per stretch
/// and <see cref="ActivityTimelineShapes.BoxHeight"/> high: a bar per stretch, rising by the square
/// root as on the timelines, orange where something clearly moved. The same as the desktop apps'
/// list (<c>ActivitySparklineBars</c>).
/// </summary>
public static class ActivitySparklineShapes
{
    /// <summary>
    /// The level from which a stretch is drawn in the timelines' event colour: a copy of Core's
    /// <c>ActivitySparkline.BusyLevel</c>, kept equal by a test.
    /// </summary>
    public const int BusyLevel = 20;

    /// <summary>
    /// Gets the bars of a sparkline.
    /// </summary>
    /// <param name="levels">The most that changed in each stretch, 0 to 100.</param>
    /// <returns>One bar per stretch with something in it, with its colour.</returns>
    public static IReadOnlyList<(TimelineRect Rect, string Fill)> Bars(
        IReadOnlyList<int> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);

        var bars = new List<(TimelineRect Rect, string Fill)>();
        for (var i = 0; i < levels.Count; i++)
        {
            var height = ActivityTimelineLayout.BarHeight(levels[i]) * ActivityTimelineShapes.BoxHeight;
            if (height > 0)
            {
                bars.Add((new TimelineRect(i + 0.1, ActivityTimelineShapes.BoxHeight - height, 0.8, height), levels[i] >= BusyLevel ? "#FF8C00" : "rgba(128,128,128,0.7)"));
            }
        }

        return bars;
    }
}