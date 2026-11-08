namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

/// <summary>
/// Notes a camera's connection changes in CameraWall's availability history, so its timeline can
/// show when a camera was offline.
/// </summary>
public interface ICameraAvailabilityNotes
{
    /// <summary>
    /// Notes a camera's new connection state; one that is neither online nor offline is not noted.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <param name="state">The state's name (<c>Connected</c>, <c>ConnectionFailed</c>, ...).</param>
    void NoteConnection(
        Guid cameraId,
        string state);
}