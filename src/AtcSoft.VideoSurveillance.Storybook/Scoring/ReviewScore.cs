namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// How well a detector did on a reviewed recording, by count: in a marked stretch with things
/// moving, did the pictures have a box for each; in a stretch marked as a false box, none.
/// </summary>
/// <param name="JudgedPictures">The pictures inside a marked stretch, past its allowance.</param>
/// <param name="Recall">Of the things that should have had a box, the share that had one (1 when none should).</param>
/// <param name="FalseBoxes">Boxes beyond the count of things moving, in judged pictures.</param>
/// <param name="FalseBoxesPerPicture">False boxes per judged picture.</param>
/// <param name="Spans">Per marked stretch, in the review's order.</param>
public sealed record ReviewScore(
    int JudgedPictures,
    double Recall,
    int FalseBoxes,
    double FalseBoxesPerPicture,
    IReadOnlyList<ReviewSpanScore> Spans)
{
    /// <summary>
    /// Gets the score as a short line.
    /// </summary>
    public string Summary
        => string.Create(
            CultureInfo.InvariantCulture,
            $"recall {Recall:0.000}, false boxes/picture {FalseBoxesPerPicture:0.000} ({FalseBoxes}), {JudgedPictures} pictures judged in {Spans.Count} stretches");
}