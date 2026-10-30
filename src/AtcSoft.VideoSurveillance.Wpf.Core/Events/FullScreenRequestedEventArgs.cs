namespace AtcSoft.VideoSurveillance.Wpf.Core.Events;

/// <summary>
/// Event arguments for full screen camera requests.
/// </summary>
public class FullScreenRequestedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FullScreenRequestedEventArgs"/> class.
    /// </summary>
    /// <param name="camera">The camera to display in full screen.</param>
    /// <param name="sourceTile">The source camera tile control (for player lending).</param>
    public FullScreenRequestedEventArgs(
        CameraConfiguration camera,
        CameraTile? sourceTile)
    {
        Camera = camera ?? throw new ArgumentNullException(nameof(camera));
        SourceTile = sourceTile;
    }

    /// <summary>
    /// Gets the camera to display in full screen.
    /// </summary>
    public CameraConfiguration Camera { get; }

    /// <summary>
    /// Gets the source camera tile control.
    /// Used for player lending to enable instant fullscreen without stream reconnection.
    /// </summary>
    public CameraTile? SourceTile { get; }
}