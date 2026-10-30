namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Shrinks an activity index to the width a timeline draws. Each stretch keeps the <b>most</b>
/// that happened in it, never the average: at an hour on a thousand pixels a pixel is 3.6 s, and
/// an average would wipe out a person walking by for five seconds.
/// </summary>
public static class ActivityDownsampler
{
    /// <summary>
    /// Splits the recording into <paramref name="buckets"/> equal stretches and keeps the most of each.
    /// </summary>
    /// <param name="log">The activity index.</param>
    /// <param name="durationSeconds">
    /// How long the recording is, in seconds; 0 or less means as long as the index reaches.
    /// </param>
    /// <param name="buckets">How many stretches, at least 1.</param>
    /// <returns>The stretches, from the start.</returns>
    public static IReadOnlyList<ActivityBucket> ByMax(
        ActivityLog log,
        double durationSeconds,
        int buckets)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentOutOfRangeException.ThrowIfLessThan(buckets, 1);

        var duration = durationSeconds > 0 ? durationSeconds : log.DurationSeconds;
        var result = Enumerable.Repeat(ActivityBucket.NotAnalysed, buckets).ToArray();
        if (duration <= 0)
        {
            return result;
        }

        foreach (var second in log.Seconds)
        {
            if (second.Offset >= duration)
            {
                break;
            }

            // A second may cover more than one stretch when there are more stretches than seconds.
            var first = (int)Math.Floor(second.Offset * buckets / duration);
            var last = (int)Math.Ceiling(Math.Min(second.Offset + 1, duration) * buckets / duration) - 1;
            for (var i = Math.Max(first, 0); i <= Math.Min(last, buckets - 1); i++)
            {
                var bucket = result[i];
                result[i] = new ActivityBucket(
                    Math.Max(bucket.Level, second.Level),
                    Math.Max(bucket.Tracks, second.Tracks),
                    IsAnalysed: true)
                {
                    Zones = second.Zones.Count == 0
                        ? bucket.Zones
                        : [.. bucket.Zones.Concat(second.Zones).Distinct(StringComparer.Ordinal)],
                };
            }
        }

        return result;
    }
}