namespace AtcSoft.VideoSurveillance.Enums;

/// <summary>
/// Specifies when automatic media cleanup should run.
/// </summary>
public enum MediaCleanupSchedule
{
    /// <summary>
    /// Automatic cleanup is disabled.
    /// </summary>
    Disabled,

    /// <summary>
    /// Cleanup runs once when the application starts.
    /// </summary>
    OnStartup,

    /// <summary>
    /// Cleanup runs on application startup and periodically (every 6 hours) while running.
    /// </summary>
    OnStartupAndPeriodically,
}