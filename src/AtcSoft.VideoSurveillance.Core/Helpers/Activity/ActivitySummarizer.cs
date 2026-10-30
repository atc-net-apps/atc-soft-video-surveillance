namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Shortens a recording's activity index for timelines over hours or days: ten-second stretches
/// with the most of each, and the events to the second.
/// </summary>
public static class ActivitySummarizer
{
    /// <summary>
    /// The length of a stretch, in seconds.
    /// </summary>
    public const int BucketSeconds = 10;

    /// <summary>
    /// Moving seconds this close together are one event: someone stopping for a moment is not
    /// two visits.
    /// </summary>
    public const int MergeGapSeconds = 5;

    /// <summary>
    /// Shortens an activity index.
    /// </summary>
    /// <param name="log">The index.</param>
    /// <returns>The summary.</returns>
    public static ActivitySummary Summarize(ActivityLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        var duration = log.DurationSeconds;
        var count = (duration + BucketSeconds - 1) / BucketSeconds;
        var level = new double[count];
        var tracks = new int[count];
        var analysed = new bool[count];
        var events = new List<ActivitySpan>();
        ActivitySpan? open = null;

        foreach (var second in log.Seconds)
        {
            var i = second.Offset / BucketSeconds;
            level[i] = Math.Max(level[i], second.Level);
            tracks[i] = Math.Max(tracks[i], second.Tracks);
            analysed[i] = true;

            if (second.Tracks <= 0)
            {
                continue;
            }

            if (open is not null && second.Offset - open.EndOffset <= MergeGapSeconds)
            {
                open = open with
                {
                    EndOffset = second.Offset + 1,
                    PeakLevel = Math.Max(open.PeakLevel, second.Level),
                    Tracks = Math.Max(open.Tracks, second.Tracks),
                };
            }
            else
            {
                if (open is not null)
                {
                    events.Add(open);
                }

                open = new ActivitySpan(second.Offset, second.Offset + 1, second.Level, second.Tracks);
            }
        }

        if (open is not null)
        {
            events.Add(open);
        }

        var buckets = new ActivityBucket[count];
        for (var i = 0; i < count; i++)
        {
            buckets[i] = analysed[i] ? new ActivityBucket(level[i], tracks[i], IsAnalysed: true) : ActivityBucket.NotAnalysed;
        }

        return new ActivitySummary(log.Start, duration, buckets, events);
    }
}