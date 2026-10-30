namespace AtcSoft.VideoSurveillance.Helpers.Review;

/// <summary>
/// A verdict a timeline offers for a stretch of a recording.
/// </summary>
/// <param name="Verdict">One of <see cref="ReviewVerdicts"/>.</param>
/// <param name="FromSeconds">Where the stretch starts, in seconds into the recording.</param>
/// <param name="ToSeconds">Where it ends.</param>
public sealed record TimelineVerdict(
    string Verdict,
    double FromSeconds,
    double ToSeconds)
{
    /// <summary>
    /// Gets the stretch as a review's span.
    /// </summary>
    public ReviewSpan ToSpan()
        => new()
        {
            From = TimeSpan.FromSeconds(FromSeconds),
            To = TimeSpan.FromSeconds(ToSeconds),
            Verdict = Verdict,
        };
}