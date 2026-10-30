namespace AtcSoft.VideoSurveillance.Events;

/// <summary>
/// Event arguments for camera connection state changes.
/// </summary>
public class CameraConnectionChangedEventArgs : EventArgs
{
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

    public CameraConfiguration Camera { get; }

    public ConnectionState PreviousState { get; }

    public ConnectionState NewState { get; }

    public string? ErrorMessage { get; }

    public DateTime Timestamp { get; }

    /// <inheritdoc />
    public override string ToString()
        => $"CameraConnectionChanged {{ CameraId={Camera.Id.ToString().Substring(0, 8)}, {PreviousState} -> {NewState} }}";
}