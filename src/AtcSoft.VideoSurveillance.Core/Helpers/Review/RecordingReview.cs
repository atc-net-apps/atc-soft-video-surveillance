namespace AtcSoft.VideoSurveillance.Helpers.Review;

/// <summary>
/// A person's verdicts on a real recording (<c>{video}.review.json</c>): stretches of it marked as
/// correctly boxed, falsely boxed, missed or boxed with the wrong count, and the detector settings
/// it was judged with. A review is the truth of a real recording: the marked stretches are judged,
/// the rest is not.
/// </summary>
public sealed class RecordingReview
{
    /// <summary>
    /// The format's version.
    /// </summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// Gets or sets the format's version.
    /// </summary>
    public int Version { get; set; } = FormatVersion;

    /// <summary>
    /// Gets or sets the recording the review is of.
    /// </summary>
    public ReviewRecording Recording { get; set; } = new();

    /// <summary>
    /// Gets or sets how the recording is analysed: the camera's settings and zones when it was reviewed.
    /// </summary>
    public ReviewDetector Detector { get; set; } = new();

    /// <summary>
    /// Gets the marked stretches, each with its verdict.
    /// </summary>
    public IList<ReviewSpan> Spans { get; init; } = [];
}