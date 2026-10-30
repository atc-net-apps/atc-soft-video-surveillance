namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// The recordings an app is making right now, and the pipelines they are made on: the part of a
/// recording service that what follows the recordings (the activity index) needs, the same in
/// CameraWall and on the server.
/// </summary>
public interface IActiveRecordings
{
    /// <summary>
    /// Occurs when the recording state changes for a camera.
    /// </summary>
    event EventHandler<RecordingStateChangedEventArgs>? RecordingStateChanged;

    /// <summary>
    /// Gets the active recording session for a camera, if any.
    /// </summary>
    /// <param name="cameraId">The camera ID.</param>
    /// <returns>The recording session, or null if not recording.</returns>
    RecordingSession? GetSession(Guid cameraId);

    /// <summary>
    /// Gets the pipeline a camera is being recorded on.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <returns>The pipeline, or <see langword="null"/> when the camera is not recording.</returns>
    IMediaPipeline? FindPipeline(Guid cameraId);

    /// <summary>
    /// Checks if a camera is currently recording.
    /// </summary>
    /// <param name="cameraId">The camera ID.</param>
    /// <returns>True if recording; otherwise, false.</returns>
    bool IsRecording(Guid cameraId);

    /// <summary>
    /// Gets all active recording sessions.
    /// </summary>
    /// <returns>A read-only list of active recording sessions.</returns>
    IReadOnlyList<RecordingSession> GetActiveSessions();
}