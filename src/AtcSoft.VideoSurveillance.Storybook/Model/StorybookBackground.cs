namespace AtcSoft.VideoSurveillance.Storybook.Model;

/// <summary>
/// The still scene and the things in it that change but must give no box.
/// </summary>
public sealed class StorybookBackground
{
    /// <summary>
    /// Gets or sets the seed: the same seed draws the same scene, frame for frame.
    /// </summary>
    public int Seed { get; set; } = 42;

    public IList<StorybookDistractor> Distractors { get; init; } = [];
}