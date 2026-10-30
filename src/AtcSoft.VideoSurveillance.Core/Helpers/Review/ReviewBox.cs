namespace AtcSoft.VideoSurveillance.Helpers.Review;

/// <summary>
/// Where something was in one picture of a recording, as shares of the picture as displayed.
/// </summary>
public sealed class ReviewBox
{
    /// <summary>
    /// Gets or sets the picture's position in the video.
    /// </summary>
    public TimeSpan At { get; set; }

    /// <summary>
    /// Gets or sets the left edge, 0 to 1.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Gets or sets the top edge, 0 to 1.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Gets or sets the width, 0 to 1.
    /// </summary>
    public double W { get; set; }

    /// <summary>
    /// Gets or sets the height, 0 to 1.
    /// </summary>
    public double H { get; set; }
}