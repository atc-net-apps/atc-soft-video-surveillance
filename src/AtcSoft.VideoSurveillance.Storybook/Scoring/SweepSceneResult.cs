namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// How a combination of settings did on one scene at one rate.
/// </summary>
/// <param name="Scene">The scene's name.</param>
/// <param name="AnalysesPerSecond">The rate.</param>
/// <param name="IsKnownBad">Whether the scene is marked known-bad (not judged).</param>
/// <param name="Meets">Whether the run meets the scene's expectations.</param>
/// <param name="Recall">The recall.</param>
/// <param name="FalseBoxesPerFrame">The false boxes per frame.</param>
public sealed record SweepSceneResult(
    string Scene,
    double AnalysesPerSecond,
    bool IsKnownBad,
    bool Meets,
    double Recall,
    double FalseBoxesPerFrame);