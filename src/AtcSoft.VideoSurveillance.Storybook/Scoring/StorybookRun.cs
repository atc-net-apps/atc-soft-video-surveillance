namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// A scene run through a detector: the score and every analysed frame.
/// </summary>
/// <param name="Score">The score.</param>
/// <param name="Frames">The analysed frames with their truth and the boxes shown.</param>
public sealed record StorybookRun(
    StorybookScore Score,
    IReadOnlyList<ScoredFrame> Frames)
{
    /// <summary>
    /// Gets the width of the pictures the detector was given.
    /// </summary>
    public int AnalysisWidth { get; init; }

    /// <summary>
    /// Gets the height of the pictures the detector was given.
    /// </summary>
    public int AnalysisHeight { get; init; }

    /// <summary>
    /// Gets the width of the video, as shown: the boxes and the truth are in its pixels.
    /// </summary>
    public int VideoWidth { get; init; }

    /// <summary>
    /// Gets the height of the video, as shown.
    /// </summary>
    public int VideoHeight { get; init; }
}