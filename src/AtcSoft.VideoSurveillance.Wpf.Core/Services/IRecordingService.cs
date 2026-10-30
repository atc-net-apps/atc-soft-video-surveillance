namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// Service for managing camera recording sessions.
/// </summary>
public interface IRecordingService : IActiveRecordings
{
    /// <summary>
    /// Gets the current recording state for a camera.
    /// </summary>
    /// <param name="cameraId">The camera ID.</param>
    /// <returns>The current recording state.</returns>
    RecordingState GetRecordingState(Guid cameraId);

    /// <summary>
    /// Starts a manual recording for a camera.
    /// </summary>
    /// <param name="camera">The camera configuration.</param>
    /// <param name="pipeline">The media pipeline instance.</param>
    /// <returns>True if recording started successfully; otherwise, false.</returns>
    bool StartRecording(
        CameraConfiguration camera,
        IMediaPipeline pipeline);

    /// <summary>
    /// Stops recording for a camera.
    /// </summary>
    /// <param name="cameraId">The camera ID.</param>
    void StopRecording(Guid cameraId);

    /// <summary>
    /// Triggers motion-based recording for a camera.
    /// </summary>
    /// <param name="camera">The camera configuration.</param>
    /// <param name="pipeline">The media pipeline instance.</param>
    /// <returns>True if recording started or was already in progress; otherwise, false.</returns>
    bool TriggerMotionRecording(
        CameraConfiguration camera,
        IMediaPipeline pipeline);

    /// <summary>
    /// Updates the last motion timestamp for an active motion recording.
    /// This extends the post-motion recording period.
    /// </summary>
    /// <param name="cameraId">The camera ID.</param>
    void UpdateMotionTimestamp(Guid cameraId);

    /// <summary>
    /// Generates a recording filename for a camera.
    /// </summary>
    /// <param name="camera">The camera configuration.</param>
    /// <param name="format">The recording format (mp4, mkv, avi).</param>
    /// <returns>The full path to the recording file.</returns>
    string GenerateRecordingFilename(
        CameraConfiguration camera,
        string format);

    /// <summary>
    /// Stops all active recordings. Call this when the application is shutting down
    /// to ensure all recording files are properly finalized.
    /// </summary>
    void StopAllRecordings();

    /// <summary>
    /// Segments an active recording by stopping the current recording and starting a new one
    /// with a new filename. The recording type (manual or motion) is preserved.
    /// </summary>
    /// <param name="cameraId">The camera ID.</param>
    /// <returns>True if segmentation was successful; otherwise, false.</returns>
    bool SegmentRecording(Guid cameraId);

    /// <summary>
    /// Checks disk space mid-recording. Attempts to reclaim space; if the
    /// drive is still below the guard threshold after reclaim, stops all
    /// active recordings to prevent disk exhaustion.
    /// </summary>
    /// <returns>
    /// True if recordings were stopped; false otherwise.
    /// Returns false when the disk-space guard is disabled or no recordings are active.
    /// </returns>
    bool EnforceDiskSpaceGuard();

    /// <summary>
    /// Keeps free space on every volume that holds media (recordings, snapshots, time-lapse
    /// pictures), deleting the oldest first, and says what it found on each.
    /// </summary>
    /// <returns>What was found and done; <see cref="DiskSpaceGuardResult.None"/> when the guard is off.</returns>
    DiskSpaceGuardResult ReclaimDiskSpace()
        => DiskSpaceGuardResult.None;
}