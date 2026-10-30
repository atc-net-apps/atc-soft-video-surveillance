namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// How a combination of settings did on one reviewed recording.
/// </summary>
/// <param name="Recording">The recording's file name.</param>
/// <param name="Recall">The recall by its review.</param>
/// <param name="FalseBoxesPerPicture">The false boxes per judged picture.</param>
public sealed record SweepCorpusResult(
    string Recording,
    double Recall,
    double FalseBoxesPerPicture);