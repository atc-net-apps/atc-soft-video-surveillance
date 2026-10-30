namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// A reviewed recording a sweep runs.
/// </summary>
/// <param name="VideoPath">The recording.</param>
/// <param name="Review">Its review.</param>
public sealed record SweepRecording(
    string VideoPath,
    RecordingReview Review);