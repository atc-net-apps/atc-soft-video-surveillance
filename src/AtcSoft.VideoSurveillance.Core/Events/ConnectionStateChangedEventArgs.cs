namespace AtcSoft.VideoSurveillance.Events;

/// <summary>
/// Event arguments for media pipeline connection state changes.
/// </summary>
public class ConnectionStateChangedEventArgs : EventArgs
{
    public ConnectionStateChangedEventArgs(
        ConnectionState previousState,
        ConnectionState newState,
        string? errorMessage = null)
    {
        PreviousState = previousState;
        NewState = newState;
        ErrorMessage = errorMessage;
        Timestamp = DateTime.UtcNow;
    }

    public ConnectionState PreviousState { get; }

    public ConnectionState NewState { get; }

    public string? ErrorMessage { get; }

    public DateTime Timestamp { get; }

    /// <inheritdoc />
    public override string ToString()
        => $"ConnectionStateChanged {{ {PreviousState} -> {NewState} }}";
}