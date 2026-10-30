namespace AtcSoft.VideoSurveillance.Enums;

/// <summary>
/// Why a stretch of a camera's timeline has no recording.
/// </summary>
public enum GapReason
{
    /// <summary>
    /// Nothing says why: no history reaches that far back.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The camera was offline.
    /// </summary>
    Offline = 1,

    /// <summary>
    /// The camera was there but not recorded (not set to record, or stopped by hand).
    /// </summary>
    NotRecording = 2,

    /// <summary>
    /// The disk was full: the recording was stopped or refused.
    /// </summary>
    DiskFull = 3,

    /// <summary>
    /// The server was not running.
    /// </summary>
    ServerDown = 4,
}