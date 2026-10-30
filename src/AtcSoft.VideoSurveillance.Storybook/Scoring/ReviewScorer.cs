namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// Scores a run of a recording against its review, by count: a review marks stretches, not boxes.
/// Only pictures inside a marked stretch are judged. A picture expects as many boxes as the
/// stretches over it have things moving (none in a stretch marked as a false box); a box beyond
/// that is false. The first moments of a stretch with things moving are not judged: a person marks
/// from when a thing appears, and the detector needs a moment to be sure of it.
/// </summary>
public static class ReviewScorer
{
    /// <summary>
    /// How long after the start of a stretch with things moving its pictures are not judged.
    /// </summary>
    public const double DefaultAllowanceSeconds = 1.0;

    /// <summary>
    /// Scores a run.
    /// </summary>
    /// <param name="frames">The analysed pictures, in time order, at their positions in the video.</param>
    /// <param name="review">The recording's review.</param>
    /// <param name="allowanceSeconds">How long after the start of a stretch with things moving its pictures are not judged.</param>
    /// <returns>The score.</returns>
    public static ReviewScore Score(
        IReadOnlyList<ScoredFrame> frames,
        RecordingReview review,
        double allowanceSeconds = DefaultAllowanceSeconds)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(review);

        var spans = review.Spans;
        var pictures = new int[spans.Count];
        var expected = new int[spans.Count];
        var hits = new int[spans.Count];
        var falseBySpan = new int[spans.Count];
        var judged = 0;
        var allExpected = 0;
        var allHits = 0;
        var allFalse = 0;

        foreach (var frame in frames)
        {
            var over = Enumerable.Range(0, spans.Count).Where(i => spans[i].From.TotalSeconds <= frame.Time && frame.Time < spans[i].To.TotalSeconds).ToList();
            if (over.Count == 0 || over.Any(i => ReviewVerdicts.ExpectedCount(spans[i]) > 0 && frame.Time < spans[i].From.TotalSeconds + allowanceSeconds))
            {
                continue;
            }

            var want = over.Sum(i => ReviewVerdicts.ExpectedCount(spans[i]));
            var boxes = frame.Detected.Count;
            var hit = Math.Min(boxes, want);
            var extra = Math.Max(0, boxes - want);
            judged++;
            allExpected += want;
            allHits += hit;
            allFalse += extra;
            foreach (var i in over)
            {
                pictures[i]++;
                expected[i] += ReviewVerdicts.ExpectedCount(spans[i]);
                hits[i] += Math.Min(boxes, ReviewVerdicts.ExpectedCount(spans[i]));
                falseBySpan[i] += extra;
            }
        }

        return new ReviewScore(
            judged,
            Share(allHits, allExpected),
            allFalse,
            judged == 0 ? 0 : allFalse / (double)judged,
            [.. Enumerable.Range(0, spans.Count).Select(i => new ReviewSpanScore(spans[i].Verdict, pictures[i], Share(hits[i], expected[i]), falseBySpan[i]))]);
    }

    private static double Share(
        int part,
        int whole)
        => whole == 0 ? 1 : part / (double)whole;
}