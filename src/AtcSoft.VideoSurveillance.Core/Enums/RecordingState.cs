namespace AtcSoft.VideoSurveillance.Enums;

/// <summary>
/// Specifies the current recording state for a camera.
/// </summary>
public enum RecordingState
{
    /// <summary>
    /// Camera is not recording.
    /// </summary>
    Idle,

    /// <summary>
    /// Camera is actively recording (manual recording).
    /// </summary>
    Recording,

    /// <summary>
    /// Camera is recording due to motion detection.
    /// </summary>
    RecordingMotion,

    /// <summary>
    /// Camera is recording post-motion (motion stopped but still recording for configured duration).
    /// </summary>
    RecordingPostMotion,
}