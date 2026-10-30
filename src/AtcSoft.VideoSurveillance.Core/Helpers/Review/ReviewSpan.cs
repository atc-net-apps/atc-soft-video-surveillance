namespace AtcSoft.VideoSurveillance.Helpers.Review;

/// <summary>
/// A marked stretch of a recording. Its times are positions in the video, from its first picture
/// (as a player shows them), not times of day and not the activity index's seconds, which may run
/// up to a key-frame interval ahead of the video.
/// </summary>
public sealed class ReviewSpan
{
    /// <summary>
    /// Gets or sets where it starts.
    /// </summary>
    public TimeSpan From { get; set; }

    /// <summary>
    /// Gets or sets where it ends.
    /// </summary>
    public TimeSpan To { get; set; }

    /// <summary>
    /// Gets or sets the verdict: one of <see cref="ReviewVerdicts"/>.
    /// </summary>
    public string Verdict { get; set; } = ReviewVerdicts.Correct;

    /// <summary>
    /// Gets or sets how many moving things there are in the stretch (1 when not given); never for
    /// <see cref="ReviewVerdicts.FalseBox"/>.
    /// </summary>
    public int? Count { get; set; }

    /// <summary>
    /// Gets or sets what moves (person, car, cat, ...), or <see langword="null"/>.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets a note (a tree, the camera's clock, ...), or <see langword="null"/>.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Gets or sets where the thing missed was in one picture, or <see langword="null"/>.
    /// </summary>
    public ReviewBox? Box { get; set; }
}