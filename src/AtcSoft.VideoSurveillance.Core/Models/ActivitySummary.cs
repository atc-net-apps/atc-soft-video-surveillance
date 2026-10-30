namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// The activity of one recording in short: ten-second stretches with the most of each, and its
/// events to the second. What a timeline over hours or days needs, at a fraction of the index.
/// </summary>
/// <param name="Start">When the index starts, in UTC.</param>
/// <param name="DurationSeconds">How many seconds the index covers.</param>
/// <param name="Buckets">Stretches of <c>ActivitySummarizer.BucketSeconds</c> each, from the start.</param>
/// <param name="Events">The stretches in which something moved, from the start.</param>
public sealed record ActivitySummary(
    DateTimeOffset Start,
    int DurationSeconds,
    IReadOnlyList<ActivityBucket> Buckets,
    IReadOnlyList<ActivitySpan> Events);