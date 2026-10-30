namespace AtcSoft.VideoSurveillance.Storybook.Model;

/// <summary>
/// Where an object's centre is at a time.
/// </summary>
public sealed class StorybookKeyframe
{
    /// <summary>
    /// Gets or sets the time in seconds.
    /// </summary>
    public double T { get; set; }

    /// <summary>
    /// Gets or sets the centre, as fractions of the picture (below 0 or above 1 is outside it).
    /// </summary>
    public IList<double> At { get; init; } = [0.5, 0.5];

    /// <summary>
    /// Gets or sets the size at this time (width and height as fractions), for something coming
    /// towards the camera or going away; the object's own size when not given.
    /// </summary>
    public IList<double>? Size { get; init; }
}