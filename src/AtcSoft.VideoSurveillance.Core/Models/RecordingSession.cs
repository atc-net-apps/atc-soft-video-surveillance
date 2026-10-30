namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// Tracks an active recording session for a camera.
/// </summary>
public class RecordingSession
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingSession"/> class.
    /// </summary>
    public RecordingSession(
        Guid cameraId,
        string cameraName,
        string filePath,
        bool isManualRecording)
    {
        CameraId = cameraId;
        CameraName = cameraName ?? throw new ArgumentNullException(nameof(cameraName));
        CurrentFilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        IsManualRecording = isManualRecording;
        State = isManualRecording ? RecordingState.Recording : RecordingState.RecordingMotion;
        StartTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the ID of the camera being recorded.
    /// </summary>
    public Guid CameraId { get; }

    /// <summary>
    /// Gets the display name of the camera being recorded. Used for log
    /// readability so operators don't have to translate GUIDs by hand.
    /// </summary>
    public string CameraName { get; }

    /// <summary>
    /// Gets or sets the current recording state.
    /// </summary>
    public RecordingState State { get; set; }

    /// <summary>
    /// Gets the file path of the current recording.
    /// </summary>
    public string CurrentFilePath { get; }

    /// <summary>
    /// Gets the time when the recording started.
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    /// Gets a value indicating whether this is a manual recording.
    /// </summary>
    public bool IsManualRecording { get; }

    /// <summary>
    /// Gets or sets the last time motion was detected.
    /// </summary>
    public DateTime? LastMotionTime { get; set; }

    /// <summary>
    /// Gets the current recording duration.
    /// </summary>
    public TimeSpan Duration => DateTime.UtcNow - StartTime;

    /// <inheritdoc />
    public override string ToString()
        => $"RecordingSession {{ Camera='{CameraName}', State={State}, Manual={IsManualRecording} }}";
}