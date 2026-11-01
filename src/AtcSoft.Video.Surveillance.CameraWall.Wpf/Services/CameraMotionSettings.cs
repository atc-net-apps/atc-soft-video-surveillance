namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

/// <summary>
/// A camera's motion settings: its own over the app's, as its tile resolves them.
/// </summary>
internal static class CameraMotionSettings
{
    public static MotionDetectionSettings Resolve(
        CameraConfiguration camera,
        MotionDetectionSettings defaults)
    {
        var overrides = camera.Overrides?.MotionDetection;
        return new MotionDetectionSettings
        {
            Sensitivity = overrides?.Sensitivity ?? defaults.Sensitivity,
            SmallestObjectPercent = overrides?.SmallestObjectPercent ?? defaults.SmallestObjectPercent,
            StoppedObjectSeconds = overrides?.StoppedObjectSeconds ?? defaults.StoppedObjectSeconds,
            IgnoreLightingChanges = overrides?.IgnoreLightingChanges ?? defaults.IgnoreLightingChanges,
            MinimumChangePercent = overrides?.MinimumChangePercent ?? defaults.MinimumChangePercent,
            AnalysisFrameRate = overrides?.AnalysisFrameRate ?? defaults.AnalysisFrameRate,
            AnalysisWidth = overrides?.AnalysisWidth is > 0 and var ownWidth ? ownWidth : defaults.AnalysisWidth,
            PostMotionDurationSeconds = overrides?.PostMotionDurationSeconds ?? defaults.PostMotionDurationSeconds,
            CooldownSeconds = overrides?.CooldownSeconds ?? defaults.CooldownSeconds,
        };
    }
}