namespace AtcSoft.Video.Surveillance.Api.Domain.Streaming;

/// <summary>
/// The cameras someone asked to record (<c>POST /cameras/{id}/recording</c>) and has not asked
/// to stop. Kept in the data folder, so such a recording goes on after the camera drops out and
/// after the server restarts, as a camera recorded on connect does.
/// </summary>
public interface IRecordingIntents
{
    /// <summary>
    /// Gets whether someone asked to record the camera and has not asked to stop.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <returns><c>true</c> when the camera is to be recorded.</returns>
    bool IsWanted(Guid cameraId);

    /// <summary>
    /// Someone asked to record the camera.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    void Want(Guid cameraId);

    /// <summary>
    /// Someone asked to stop recording the camera, or it was deleted.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    void Drop(Guid cameraId);
}