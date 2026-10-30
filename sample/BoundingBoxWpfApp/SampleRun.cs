namespace BoundingBoxWpfApp;

/// <summary>
/// What the sample opened, run through the detector.
/// </summary>
/// <param name="Name">The storybook's name or the recording's file name.</param>
/// <param name="Way">How the pictures reached the detector: <c>fast</c>, <c>encoded</c> or <c>recording</c>.</param>
/// <param name="AnalysesPerSecond">How many pictures a second were analysed.</param>
/// <param name="Run">The frames, with their pictures, and the score.</param>
/// <param name="HasTruth">Whether there is a truth to show and score against (not for a recording).</param>
/// <param name="ScoreJson">The score as JSON, as shown in the score panel.</param>
/// <param name="Expect">What the storybook expects; empty for a recording.</param>
/// <param name="Detector">How the detector was made, to make it again with other settings.</param>
public sealed record SampleRun(
    string Name,
    string Way,
    double AnalysesPerSecond,
    StorybookRun Run,
    bool HasTruth,
    string ScoreJson,
    StorybookExpect Expect,
    SampleDetector Detector)
{
    /// <summary>
    /// Gets the detector's mean time a picture, in milliseconds.
    /// </summary>
    public double MeanMilliseconds
        => Run.Frames.Count == 0 ? 0 : Run.Frames.Average(f => f.DetectorMilliseconds);

    /// <summary>
    /// Gets the detector's longest time over a picture, in milliseconds.
    /// </summary>
    public double MaxMilliseconds
        => Run.Frames.Count == 0 ? 0 : Run.Frames.Max(f => f.DetectorMilliseconds);
}