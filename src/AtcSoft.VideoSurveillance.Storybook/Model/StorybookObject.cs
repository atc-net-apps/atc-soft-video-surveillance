namespace AtcSoft.VideoSurveillance.Storybook.Model;

/// <summary>
/// A moving thing a detector must find: a car, a human, a cat or a dog.
/// </summary>
public sealed class StorybookObject
{
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kind: <c>car</c>, <c>human</c>, <c>cat</c> or <c>dog</c>.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the width and height, as fractions of the picture (up to 3: someone close to
    /// the lens is larger than the picture).
    /// </summary>
    public IList<double> Size { get; init; } = [0.05, 0.18];

    /// <summary>
    /// Gets or sets where it is when: key points, straight lines between them. Before the first
    /// and after the last it is not in the picture.
    /// </summary>
    public IList<StorybookKeyframe> Path { get; init; } = [];

    /// <summary>
    /// Gets or sets the grey level of a cat or a dog, 0 to 255, for a faint animal close to the
    /// background's grey; <see langword="null"/> draws a cat dark (40) and a dog light (150).
    /// </summary>
    public int? Shade { get; set; }
}