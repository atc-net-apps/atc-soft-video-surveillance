namespace AtcSoft.VideoSurveillance.Events;

/// <summary>
/// Event arguments for recording state changes.
/// </summary>
public class RecordingStateChangedEventArgs : EventArgs
{
    public RecordingStateChangedEventArgs(
        Guid cameraId,
        RecordingState oldState,
        RecordingState newState,
        string? filePath = null)
    {
        CameraId = cameraId;
        OldState = oldState;
        NewState = newState;
        FilePath = filePath;
        Timestamp = DateTime.UtcNow;
    }

    public Guid CameraId { get; }

    public RecordingState OldState { get; }

    public RecordingState NewState { get; }

    public string? FilePath { get; }

    public DateTime Timestamp { get; }

    /// <inheritdoc />
    public override string ToString()
        => $"RecordingStateChanged {{ CameraId={CameraId.ToString()[..8]}, {OldState} -> {NewState} }}";
}