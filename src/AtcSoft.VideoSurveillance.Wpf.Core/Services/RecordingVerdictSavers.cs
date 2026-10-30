namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// The ways a verdict on a recording is saved.
/// </summary>
public static class RecordingVerdictSavers
{
    /// <summary>
    /// Writes the review beside a recording on this machine, for a camera as the app stores it.
    /// </summary>
    /// <param name="videoPath">The recording.</param>
    /// <param name="camera">The camera the recording is of, or <see langword="null"/>.</param>
    /// <param name="appSettings">The app's motion settings.</param>
    /// <returns>The saver.</returns>
    public static RecordingVerdictSaver ToFile(
        string videoPath,
        CameraConfiguration? camera,
        MotionDetectionSettings appSettings)
        => ToFile(videoPath, camera?.Core, appSettings);

    /// <summary>
    /// Writes the review beside a recording on this machine. A new review gets the camera's motion
    /// settings (the app's when the camera is not known) and its zones, turned to the rotation the
    /// recording was made at, as *Analyse recording* uses them.
    /// </summary>
    /// <param name="videoPath">The recording.</param>
    /// <param name="camera">The camera the recording is of, or <see langword="null"/>.</param>
    /// <param name="appSettings">The app's motion settings.</param>
    /// <returns>The saver.</returns>
    public static RecordingVerdictSaver ToFile(
        string videoPath,
        AtcSoft.VideoSurveillance.Models.CameraConfiguration? camera,
        MotionDetectionSettings appSettings)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);
        ArgumentNullException.ThrowIfNull(appSettings);

        return (verdict, cancellationToken) => Task.Run(
            () =>
            {
                var settings = RecordingAnalysis.SettingsFor(camera, appSettings);
                var zones = RecordingAnalysis.ZonesFor(camera, MediaProbe.GetRotation(videoPath));
                RecordingReviews.AddVerdict(videoPath, verdict.ToSpan(), settings, zones);
            },
            cancellationToken);
    }
}