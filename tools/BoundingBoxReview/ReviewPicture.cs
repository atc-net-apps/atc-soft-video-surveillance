namespace BoundingBoxReview;

/// <summary>
/// One picture of a review: where in the recording it is, the detector's boxes on it, and the
/// boxes the person drew (<see langword="null"/> until the person has looked at it; empty when
/// nothing should be boxed).
/// </summary>
public sealed class ReviewPicture
{
    public string File { get; set; } = string.Empty;

    public double Seconds { get; set; }

    public double AnalysedSeconds { get; set; }

    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Read and written as JSON.")]
    public IList<ReviewBox> Detector { get; set; } = [];

    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Read and written as JSON.")]
    public IList<ReviewBox>? Person { get; set; }

    /// <summary>
    /// Gets or sets what the person says about the picture (what moves, what is still), or
    /// <see langword="null"/> when there is nothing new since the picture before.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; set; }

    /// <summary>
    /// Gets or sets the person's quick verdict on the yellow boxes (<see cref="ReviewVerdict"/>), or
    /// <see langword="null"/> when not given (or cleared because the detector's boxes changed).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Verdict { get; set; }
}