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
}