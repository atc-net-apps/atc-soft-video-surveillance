namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// A scene a sweep runs.
/// </summary>
/// <param name="Name">Its name in the report (its file's name).</param>
/// <param name="Scene">The storybook.</param>
public sealed record SweepScene(
    string Name,
    StorybookScene Scene);