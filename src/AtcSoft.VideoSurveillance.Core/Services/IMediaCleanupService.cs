namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Service for automatically cleaning up old recordings and snapshots.
/// </summary>
public interface IMediaCleanupService
{
    event EventHandler<MediaCleanupCompletedEventArgs>? CleanupCompleted;

    bool IsRunning { get; }

    Task<MediaCleanupResult> RunCleanupAsync();

    /// <summary>
    /// Checks free space on the recording drive and, if below the configured
    /// <see cref="MediaCleanupSettings.MinFreeSpaceMb"/> threshold, deletes the
    /// oldest recordings first until the target is met or all non-active files
    /// are exhausted. No-op when
    /// <see cref="MediaCleanupSettings.EnableDiskSpaceGuard"/> is <c>false</c>.
    /// </summary>
    Task<MediaCleanupResult> RunDiskSpaceReclaimAsync();
}