namespace AtcSoft.Video.Surveillance.Api.Domain.Streaming;

/// <summary>
/// What the server runs for each camera (its pipeline, recording, motion detection and
/// reconnect schedule), told when a camera is deleted or changed so it does not go on
/// with a camera that is gone, or with the camera as it was.
/// </summary>
public interface ICameraRuntime
{
    /// <summary>
    /// A camera was deleted: its recording is stopped (the file is kept), and its pipeline,
    /// motion detection and reconnect schedule are let go.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    void Forget(Guid cameraId);
}