namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Writes the verdicts the timelines give to the review beside a recording, with the recording's
/// camera's motion settings and zones (the server's settings when the camera is not known), the
/// zones turned to the rotation the recording was made at, as *Analyse recording* uses them.
/// </summary>
public sealed class RecordingReviewWriter(
    IApplicationSettingsService settingsService,
    ICameraStorageService cameraStorageService) : IRecordingReviewWriter
{
    /// <inheritdoc />
    public void AddVerdict(
        string videoPath,
        AtcSoft.VideoSurveillance.Helpers.Review.ReviewSpan span)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);
        ArgumentNullException.ThrowIfNull(span);

        var camera = RecordingAnalysis.CameraOf(videoPath, settingsService.Recording.RecordingPath, cameraStorageService.GetAllCameras());
        var settings = RecordingAnalysis.SettingsFor(camera, settingsService.MotionDetection);
        var zones = RecordingAnalysis.ZonesFor(camera, MediaProbe.GetRotation(videoPath));
        AtcSoft.VideoSurveillance.Helpers.Review.RecordingReviews.AddVerdict(videoPath, span, settings, zones);
    }
}