namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Resolves what a camera's motion detection runs with: the application's
/// settings, with whatever the camera itself overrides.
/// </summary>
public static class MotionDetectionPolicy
{
    /// <summary>
    /// Gets whether a camera is recorded when it sees motion.
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <param name="appDefault">The application's setting, used when the camera has none of its own.</param>
    /// <returns><see langword="true"/> when motion starts a recording of the camera.</returns>
    public static bool ShouldRecordOnMotion(
        CameraConfiguration camera,
        bool appDefault)
    {
        ArgumentNullException.ThrowIfNull(camera);
        return camera.Overrides?.Recording.EnableRecordingOnMotion ?? appDefault;
    }

    /// <summary>
    /// Gets the motion settings a camera runs with.
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <param name="appDefaults">The application's settings.</param>
    /// <returns>A copy of the settings, with the camera's overrides applied.</returns>
    public static MotionDetectionSettings Resolve(
        CameraConfiguration camera,
        MotionDetectionSettings appDefaults)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(appDefaults);

        var overrides = camera.Overrides?.MotionDetection;
        var box = overrides?.BoundingBox;

        return new MotionDetectionSettings
        {
            Sensitivity = overrides?.Sensitivity ?? appDefaults.Sensitivity,
            SmallestObjectPercent = overrides?.SmallestObjectPercent ?? appDefaults.SmallestObjectPercent,
            StoppedObjectSeconds = overrides?.StoppedObjectSeconds ?? appDefaults.StoppedObjectSeconds,
            IgnoreLightingChanges = overrides?.IgnoreLightingChanges ?? appDefaults.IgnoreLightingChanges,
            MinimumChangePercent = overrides?.MinimumChangePercent ?? appDefaults.MinimumChangePercent,
            AnalysisFrameRate = overrides?.AnalysisFrameRate ?? appDefaults.AnalysisFrameRate,
            AnalysisWidth = overrides?.AnalysisWidth ?? appDefaults.AnalysisWidth,
            AnalysisHeight = overrides?.AnalysisHeight ?? appDefaults.AnalysisHeight,
            PostMotionDurationSeconds = overrides?.PostMotionDurationSeconds ?? appDefaults.PostMotionDurationSeconds,
            CooldownSeconds = overrides?.CooldownSeconds ?? appDefaults.CooldownSeconds,
            BoundingBox = new BoundingBoxSettings
            {
                ShowInGrid = box?.ShowInGrid ?? appDefaults.BoundingBox.ShowInGrid,
                ShowInFullScreen = box?.ShowInFullScreen ?? appDefaults.BoundingBox.ShowInFullScreen,
                Color = box?.Color ?? appDefaults.BoundingBox.Color,
                Thickness = box?.Thickness ?? appDefaults.BoundingBox.Thickness,
                MinArea = box?.MinArea ?? appDefaults.BoundingBox.MinArea,
                Padding = box?.Padding ?? appDefaults.BoundingBox.Padding,
                Smoothing = box?.Smoothing ?? appDefaults.BoundingBox.Smoothing,
            },
        };
    }
}