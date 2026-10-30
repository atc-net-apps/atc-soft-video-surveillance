namespace AtcSoft.VideoSurveillance.Events;

/// <summary>
/// Event arguments for recording segmentation events.
/// </summary>
public class RecordingSegmentedEventArgs : EventArgs
{
    public RecordingSegmentedEventArgs(
        Guid cameraId,
        string previousFilePath,
        string newFilePath,
        SegmentationReason reason)
    {
        CameraId = cameraId;
        PreviousFilePath = previousFilePath ?? throw new ArgumentNullException(nameof(previousFilePath));
        NewFilePath = newFilePath ?? throw new ArgumentNullException(nameof(newFilePath));
        Reason = reason;
        Timestamp = DateTime.UtcNow;
    }

    public Guid CameraId { get; }

    public string PreviousFilePath { get; }

    public string NewFilePath { get; }

    public SegmentationReason Reason { get; }

    public DateTime Timestamp { get; }

    /// <inheritdoc />
    public override string ToString()
        => $"RecordingSegmented {{ CameraId={CameraId.ToString()[..8]}, Reason={Reason} }}";
}