namespace AtcSoft.VideoSurveillance.Helpers.Review;

/// <summary>
/// The verdicts a reviewed stretch can have.
/// </summary>
public static class ReviewVerdicts
{
    /// <summary>
    /// Boxed as it should be: <see cref="ReviewSpan.Count"/> things move.
    /// </summary>
    public const string Correct = "correct";

    /// <summary>
    /// Boxed, but nothing that matters moves (a tree, a reflection).
    /// </summary>
    public const string FalseBox = "falseBox";

    /// <summary>
    /// Something moved and was not boxed.
    /// </summary>
    public const string Missed = "missed";

    /// <summary>
    /// Boxed, with the wrong number of boxes: <see cref="ReviewSpan.Count"/> things move.
    /// </summary>
    public const string WrongCount = "wrongCount";

    /// <summary>
    /// Gets every verdict.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Correct, FalseBox, Missed, WrongCount];

    /// <summary>
    /// Gets how many things move in a stretch with this verdict: none for a false box, else the
    /// span's count (1 when not given).
    /// </summary>
    /// <param name="span">The span.</param>
    public static int ExpectedCount(ReviewSpan span)
    {
        ArgumentNullException.ThrowIfNull(span);

        return span.Verdict == FalseBox ? 0 : span.Count ?? 1;
    }
}