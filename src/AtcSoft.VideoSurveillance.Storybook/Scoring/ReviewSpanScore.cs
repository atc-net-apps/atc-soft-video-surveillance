namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// How a detector did in one marked stretch of a reviewed recording.
/// </summary>
/// <param name="Verdict">The stretch's verdict.</param>
/// <param name="Pictures">The pictures judged in it.</param>
/// <param name="Recall">Of the things that should have had a box in it, the share that had one (1 when none should).</param>
/// <param name="FalseBoxes">Boxes beyond the count of things moving in it.</param>
public sealed record ReviewSpanScore(
    string Verdict,
    int Pictures,
    double Recall,
    int FalseBoxes);