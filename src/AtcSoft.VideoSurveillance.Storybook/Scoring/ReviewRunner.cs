namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// Runs a reviewed recording as *Analyse recording* reads it (the review's settings and zones,
/// <see cref="RecordingAnalysis.WarmUpSeconds"/> of warm-up, <see cref="RecordingAnalysis.FramesPerSecond"/>
/// pictures a second, from the start of the file) and scores it against the review.
/// </summary>
public static class ReviewRunner
{
    /// <summary>
    /// Runs and scores a reviewed recording.
    /// </summary>
    /// <param name="videoPath">The recording.</param>
    /// <param name="review">Its review.</param>
    /// <param name="keepPictures">Whether each analysed picture is kept (for showing them).</param>
    /// <param name="settings">Other motion settings to run with (a sweep); the review's when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The run and its score.</returns>
    /// <exception cref="InvalidDataException">The review is of another file (its SHA-256 differs).</exception>
    public static (StorybookRun Run, ReviewScore Score) Run(
        string videoPath,
        RecordingReview review,
        bool keepPictures = false,
        MotionDetectionSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);
        ArgumentNullException.ThrowIfNull(review);

        var sha256 = RecordingReviewFile.Sha256Of(videoPath);
        if (!string.Equals(sha256, review.Recording.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"The review of {review.Recording.File} is of another file than {Path.GetFileName(videoPath)} (SHA-256 {sha256}).");
        }

        settings ??= review.Detector.Settings;
        var detector = new BuiltInMotionDetector(settings, zones: [.. review.Detector.Zones])
        {
            WarmUpSeconds = RecordingAnalysis.WarmUpSeconds,
        };
        var options = new StorybookRunOptions(
            AnalysisWidth: settings.AnalysisWidth > 0 ? settings.AnalysisWidth : MotionAnalysisSize.DefaultLongerSide,
            AnalysesPerSecond: RecordingAnalysis.FramesPerSecond,
            KeepPictures: keepPictures);
        var run = StorybookRunner.RunRecording(videoPath, detector, options, cancellationToken);
        return (run, ReviewScorer.Score(run.Frames, review));
    }
}