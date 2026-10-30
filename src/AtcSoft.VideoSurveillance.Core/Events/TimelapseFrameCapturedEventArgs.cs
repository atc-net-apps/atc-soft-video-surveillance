namespace AtcSoft.VideoSurveillance.Events;

/// <summary>
/// Event arguments for timelapse frame capture events.
/// </summary>
public class TimelapseFrameCapturedEventArgs : EventArgs
{
    public TimelapseFrameCapturedEventArgs(
        Guid cameraId,
        string filePath,
        DateTime capturedAt)
    {
        CameraId = cameraId;
        FilePath = filePath;
        CapturedAt = capturedAt;
    }

    public Guid CameraId { get; }

    public string FilePath { get; }

    public DateTime CapturedAt { get; }

    /// <inheritdoc />
    public override string ToString()
        => $"TimelapseFrameCaptured {{ CameraId={CameraId.ToString()[..8]}, CapturedAt={CapturedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} }}";
}