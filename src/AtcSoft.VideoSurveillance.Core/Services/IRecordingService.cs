namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Service for managing camera recording sessions.
/// </summary>
public interface IRecordingService : IActiveRecordings
{
    RecordingState GetRecordingState(Guid cameraId);

    bool StartRecording(
        CameraConfiguration camera,
        IMediaPipeline pipeline);

    /// <summary>
    /// Starts a recording someone asked for on a pipeline that belongs to
    /// an <see cref="ICameraPipelineHost"/>: the pipeline is left open when
    /// the recording stops.
    /// </summary>
    /// <param name="camera">The camera to record.</param>
    /// <param name="pipeline">The camera's shared pipeline.</param>
    /// <returns>
    /// <see langword="true"/> when the recording started. A recording
    /// service that owns every pipeline it records on returns
    /// <see langword="false"/>.
    /// </returns>
    bool StartRecordingOnSharedPipeline(
        CameraConfiguration camera,
        IMediaPipeline pipeline)
        => false;

    void StopRecording(Guid cameraId);

    bool TriggerMotionRecording(
        CameraConfiguration camera,
        IMediaPipeline pipeline);

    void UpdateMotionTimestamp(Guid cameraId);

    string GenerateRecordingFilename(
        CameraConfiguration camera,
        string format);

    void StopAllRecordings();

    bool SegmentRecording(Guid cameraId);

    /// <summary>
    /// Stops any active recording session whose underlying pipeline is no
    /// longer producing output (e.g. RTSP connection died, decoder exited).
    /// Implementations release the dead pipeline and mark the session
    /// stopped so a re-attempt can start a fresh recording.
    /// Returns the number of sessions reaped.
    /// </summary>
    int ReapInactiveSessions();

    /// <summary>
    /// Checks disk space mid-recording. Attempts to reclaim space; if the
    /// drive is still below the guard threshold after reclaim, stops all
    /// active recordings to prevent disk exhaustion.
    /// Returns true if recordings were stopped; false otherwise.
    /// No-op (returns false) when the disk-space guard is disabled or no
    /// recordings are active.
    /// </summary>
    bool EnforceDiskSpaceGuard();

    /// <summary>
    /// Keeps free space on every volume that holds media, deleting the oldest first, and says
    /// what it found on each.
    /// </summary>
    /// <returns>What was found and done; <see cref="DiskSpaceGuardResult.None"/> when the guard is off.</returns>
    DiskSpaceGuardResult ReclaimDiskSpace()
        => DiskSpaceGuardResult.None;
}