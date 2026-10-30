namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers;

/// <summary>
/// How starting a recording on a camera's shared pipeline went.
/// </summary>
public enum HostedCameraRecordingOutcome
{
    /// <summary>
    /// The camera is recording.
    /// </summary>
    Started = 0,

    /// <summary>
    /// The camera cannot be opened right now: it is unplugged, or its host
    /// is reconnecting to it.
    /// </summary>
    CameraUnavailable = 1,

    /// <summary>
    /// The camera was opened but delivered no picture in time.
    /// </summary>
    NotConnected = 2,

    /// <summary>
    /// The recording service did not start: the camera is recording
    /// already, or there is no room on the disk.
    /// </summary>
    NotStarted = 3,
}