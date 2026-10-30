namespace AtcSoft.VideoSurveillance.Storybook.Model;

/// <summary>
/// A play-storybook: a scene, the things moving in it, and what a detector is expected to find.
/// The renderer draws the frames from it, so the truth is exact.
/// </summary>
public sealed class StorybookScene
{
    public int Version { get; set; } = 1;

    public string Name { get; set; } = string.Empty;

    public StorybookVideo Video { get; set; } = new();

    public StorybookBackground Background { get; set; } = new();

    public IList<StorybookObject> Objects { get; init; } = [];

    /// <summary>
    /// Gets or sets the detector settings the scene is meant for, where they differ from the defaults.
    /// </summary>
    public StorybookDetector Detector { get; set; } = new();

    public StorybookExpect Expect { get; set; } = new();
}