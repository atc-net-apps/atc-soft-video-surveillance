namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// A stretch of a recording's activity, as drawn by a timeline: the most that happened in it.
/// </summary>
/// <param name="Level">The highest level of any second in the stretch, 0 to 100.</param>
/// <param name="Tracks">The most confirmed moving things at once in the stretch.</param>
/// <param name="IsAnalysed">Whether any second of the stretch was analysed.</param>
public sealed record ActivityBucket(
    double Level,
    int Tracks,
    bool IsAnalysed)
{
    /// <summary>
    /// Gets a stretch in which nothing was analysed.
    /// </summary>
    public static ActivityBucket NotAnalysed { get; } = new(0, 0, IsAnalysed: false);

    /// <summary>
    /// Gets the names of the watch zones a confirmed track was in during the stretch, in the order
    /// they were first seen.
    /// </summary>
    public IReadOnlyList<string> Zones { get; init; } = [];
}