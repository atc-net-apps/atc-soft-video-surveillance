namespace AtcSoft.VideoSurveillance.Events;

/// <summary>
/// Event arguments for camera position changes.
/// </summary>
public class CameraPositionChangedEventArgs : EventArgs
{
    public CameraPositionChangedEventArgs(
        CameraConfiguration camera,
        int previousPosition,
        int newPosition)
    {
        Camera = camera ?? throw new ArgumentNullException(nameof(camera));
        PreviousPosition = previousPosition;
        NewPosition = newPosition;
        Timestamp = DateTime.UtcNow;
    }

    public CameraConfiguration Camera { get; }

    public int PreviousPosition { get; }

    public int NewPosition { get; }

    public DateTime Timestamp { get; }

    /// <inheritdoc />
    public override string ToString()
        => $"CameraPositionChanged {{ CameraId={Camera.Id.ToString().Substring(0, 8)}, {PreviousPosition.ToString(CultureInfo.InvariantCulture)} -> {NewPosition.ToString(CultureInfo.InvariantCulture)} }}";
}