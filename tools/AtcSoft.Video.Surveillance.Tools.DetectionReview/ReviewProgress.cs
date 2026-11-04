namespace AtcSoft.Video.Surveillance.Tools.DetectionReview;

/// <summary>
/// How far making or rerunning a review has come: what it does, and the step out of how many.
/// </summary>
/// <param name="Text">What it does now, with the step ("Picture 3 of 7").</param>
/// <param name="Value">The step.</param>
/// <param name="Maximum">How many steps there are.</param>
public sealed record ReviewProgress(
    string Text,
    double Value,
    double Maximum);