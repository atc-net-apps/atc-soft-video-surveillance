namespace AtcSoft.VideoSurveillance.Enums;

/// <summary>
/// A change in whether a camera could be recorded, as the server notes it for its timeline.
/// </summary>
public enum AvailabilityChange
{
    /// <summary>
    /// The camera connected.
    /// </summary>
    Online = 0,

    /// <summary>
    /// The camera dropped out or could not be reached.
    /// </summary>
    Offline = 1,

    /// <summary>
    /// A recording of the camera started.
    /// </summary>
    RecordingStarted = 2,

    /// <summary>
    /// The recording of the camera stopped.
    /// </summary>
    RecordingStopped = 3,

    /// <summary>
    /// A recording of the camera was stopped or refused because its disk was full.
    /// </summary>
    DiskFull = 4,

    /// <summary>
    /// The server started (in the server's own log, for every camera).
    /// </summary>
    ServerStarted = 5,

    /// <summary>
    /// The server stopped (in the server's own log, for every camera).
    /// </summary>
    ServerStopped = 6,
}