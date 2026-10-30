namespace AtcSoft.VideoSurveillance.Wpf.Core.Events;

/// <summary>
/// Event arguments for camera position changes.
/// </summary>
public class CameraPositionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CameraPositionChangedEventArgs"/> class.
    /// </summary>
    /// <param name="camera">The camera whose position changed.</param>
    /// <param name="previousPosition">The previous position index.</param>
    /// <param name="newPosition">The new position index.</param>
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

    /// <summary>
    /// Gets the camera whose position changed.
    /// </summary>
    public CameraConfiguration Camera { get; }

    /// <summary>
    /// Gets the previous position index.
    /// </summary>
    public int PreviousPosition { get; }

    /// <summary>
    /// Gets the new position index.
    /// </summary>
    public int NewPosition { get; }

    /// <summary>
    /// Gets the timestamp when the position changed.
    /// </summary>
    public DateTime Timestamp { get; }
}