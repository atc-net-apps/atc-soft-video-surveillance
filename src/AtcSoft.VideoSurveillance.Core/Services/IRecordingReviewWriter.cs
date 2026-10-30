namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Adds a person's verdicts to the review beside a recording, with its camera's motion settings
/// and zones as the recording is analysed (turned to the recording's rotation) for a new review.
/// </summary>
public interface IRecordingReviewWriter
{
    /// <summary>
    /// Adds a marked stretch to a recording's review.
    /// </summary>
    /// <param name="videoPath">The recording, as a full path.</param>
    /// <param name="span">The stretch and its verdict.</param>
    /// <exception cref="InvalidDataException">The review beside the recording cannot be read, or the stretch is not valid; nothing is written.</exception>
    void AddVerdict(
        string videoPath,
        Helpers.Review.ReviewSpan span);
}