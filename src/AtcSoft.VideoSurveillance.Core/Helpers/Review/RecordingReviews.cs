namespace AtcSoft.VideoSurveillance.Helpers.Review;

/// <summary>
/// Adds verdicts to the review beside a recording (<c>{video}.review.json</c>), as the apps'
/// timelines give them. One review is written at a time, so two verdicts given at once both stay.
/// </summary>
public static class RecordingReviews
{
    private static readonly Lock WriteLock = new();

    /// <summary>
    /// Adds a marked stretch to a recording's review. A recording without a review (or whose file
    /// changed since, so its SHA-256 differs) gets a new one with the camera's settings and zones; a
    /// review kept keeps the settings it was begun with.
    /// </summary>
    /// <param name="videoPath">The recording.</param>
    /// <param name="span">The stretch and its verdict.</param>
    /// <param name="settings">The camera's motion settings, for a new review.</param>
    /// <param name="zones">The camera's motion zones turned to the recording's rotation, for a new review.</param>
    /// <returns>The review as written.</returns>
    /// <exception cref="InvalidDataException">The review beside the recording cannot be read, or the stretch is not valid; nothing is written.</exception>
    public static RecordingReview AddVerdict(
        string videoPath,
        ReviewSpan span,
        MotionDetectionSettings settings,
        IReadOnlyList<MotionZone> zones)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(zones);

        var sha256 = RecordingReviewFile.Sha256Of(videoPath);
        var path = RecordingPathHelper.BuildReviewPath(videoPath);
        lock (WriteLock)
        {
            var review = File.Exists(path) ? RecordingReviewFile.Read(path) : null;
            if (review is null || !string.Equals(review.Recording.Sha256, sha256, StringComparison.Ordinal))
            {
                review = new RecordingReview
                {
                    Recording = new ReviewRecording { File = Path.GetFileName(videoPath), Sha256 = sha256 },
                    Detector = new ReviewDetector { Settings = settings, Zones = [.. zones] },
                };
            }

            review.Spans.Add(span);
            RecordingReviewFile.Write(path, review);
            return review;
        }
    }
}