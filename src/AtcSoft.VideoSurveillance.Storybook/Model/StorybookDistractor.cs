namespace AtcSoft.VideoSurveillance.Storybook.Model;

/// <summary>
/// Something in the scene that changes but is no object: a tree in the wind, a camera clock, a
/// lighting step, rain. Which properties matter depends on <see cref="Kind"/>.
/// </summary>
public sealed class StorybookDistractor
{
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kind: <c>swayingTree</c>, <c>clockOverlay</c>, <c>brightnessStep</c>, <c>rain</c> or <c>snow</c>.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets where it stands (a tree's foot, as fractions of the picture).
    /// </summary>
    public IList<double>? At { get; init; }

    /// <summary>
    /// Gets or sets its size as a fraction of the picture's height (a tree; snow: the radius of a flake at the lens).
    /// </summary>
    public double Size { get; set; } = 0.25;

    /// <summary>
    /// Gets or sets how far it sways, as a fraction of the picture's width (a tree, snow).
    /// </summary>
    public double Amplitude { get; set; } = 0.01;

    public double PeriodSeconds { get; set; } = 1.7;

    /// <summary>
    /// Gets or sets the corner: <c>topLeft</c>, <c>topRight</c>, <c>bottomLeft</c> or <c>bottomRight</c> (a clock).
    /// </summary>
    public string Corner { get; set; } = "topRight";

    /// <summary>
    /// Gets or sets the time of day a clock shows at the start of the scene, <c>HH:MM:SS</c>.
    /// </summary>
    public string StartsAt { get; set; } = "12:00:00";

    /// <summary>
    /// Gets or sets when it happens, in seconds (a lighting step).
    /// </summary>
    public double AtSeconds { get; set; }

    /// <summary>
    /// Gets or sets the change in grey levels (a lighting step).
    /// </summary>
    public int Delta { get; set; } = 40;

    /// <summary>
    /// Gets or sets how many drops per frame (rain) or flakes in the scene (snow).
    /// </summary>
    public int Count { get; set; } = 200;
}