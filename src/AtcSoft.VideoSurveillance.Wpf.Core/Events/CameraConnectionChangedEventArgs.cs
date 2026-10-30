namespace AtcSoft.VideoSurveillance.Wpf.Core.Events;

/// <summary>
/// Event arguments for camera connection state changes.
/// </summary>
public class CameraConnectionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CameraConnectionChangedEventArgs"/> class.
    /// </summary>
    /// <param name="camera">The camera whose connection state changed.</param>
    /// <param name="previousState">The previous connection state.</param>
    /// <param name="newState">The new connection state.</param>
    /// <param name="errorMessage">An optional error message if the state is Error.</param>
    public CameraConnectionChangedEventArgs(
        CameraConfiguration camera,
        ConnectionState previousState,
        ConnectionState newState,
        string? errorMessage = null)
    {
        Camera = camera ?? throw new ArgumentNullException(nameof(camera));
        PreviousState = previousState;
        NewState = newState;
        ErrorMessage = errorMessage;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the camera whose connection state changed.
    /// </summary>
    public CameraConfiguration Camera { get; }

    /// <summary>
    /// Gets the previous connection state.
    /// </summary>
    public ConnectionState PreviousState { get; }

    /// <summary>
    /// Gets the new connection state.
    /// </summary>
    public ConnectionState NewState { get; }

    /// <summary>
    /// Gets the error message if the state is Error.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the timestamp when the state changed.
    /// </summary>
    public DateTime Timestamp { get; }
}