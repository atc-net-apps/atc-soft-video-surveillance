namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// A recording's activity in a few numbers, for a sparkline in a list of recordings: equal
/// stretches, each the most that happened in it (0 to 100), never the average, so a short event
/// still shows. Built from the activity summary, which the timelines read anyway.
/// </summary>
public static class ActivitySparkline
{
    /// <summary>
    /// How many stretches a sparkline has.
    /// </summary>
    public const int Points = 24;

    /// <summary>
    /// The level from which a stretch is drawn in the timelines' event colour: something clearly
    /// moved, not a flicker.
    /// </summary>
    public const int BusyLevel = 20;

    /// <summary>
    /// Gets a recording's sparkline.
    /// </summary>
    /// <param name="summary">The summary of its activity index, or <see langword="null"/> without one.</param>
    /// <param name="points">How many stretches.</param>
    /// <returns>The levels, rounded, or <see langword="null"/> for a recording without an index.</returns>
    public static IReadOnlyList<int>? From(
        ActivitySummary? summary,
        int points = Points)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(points, 1);

        if (summary is not { Buckets.Count: > 0 } known)
        {
            return null;
        }

        var levels = new int[points];
        var count = known.Buckets.Count;
        for (var i = 0; i < count; i++)
        {
            var bucket = known.Buckets[i];
            if (!bucket.IsAnalysed)
            {
                continue;
            }

            // Each summary stretch marks every point it overlaps.
            var first = (int)Math.Floor((double)i * points / count);
            var last = Math.Max(first, (int)Math.Ceiling((double)(i + 1) * points / count) - 1);
            var level = (int)Math.Round(Math.Clamp(bucket.Level, 0, 100), MidpointRounding.AwayFromZero);
            for (var p = first; p <= Math.Min(last, points - 1); p++)
            {
                levels[p] = Math.Max(levels[p], level);
            }
        }

        return levels;
    }
}