namespace AtcSoft.VideoEngine;

/// <summary>
/// Event arguments for player state changes.
/// </summary>
public sealed class PlayerStateChangedEventArgs : EventArgs
{
    public PlayerStateChangedEventArgs(
        PlayerState previousState,
        PlayerState newState,
        string? errorMessage = null,
        StreamFailureReason reason = StreamFailureReason.Unknown)
    {
        PreviousState = previousState;
        NewState = newState;
        ErrorMessage = errorMessage;
        Reason = reason;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the previous state.
    /// </summary>
    public PlayerState PreviousState { get; }

    /// <summary>
    /// Gets the new state.
    /// </summary>
    public PlayerState NewState { get; }

    /// <summary>
    /// Gets the error message when transitioning to <see cref="PlayerState.Error"/>.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Classified failure cause when <see cref="NewState"/> is
    /// <see cref="PlayerState.Error"/>; <see cref="StreamFailureReason.Unknown"/>
    /// for all other transitions. Lets the UI render a distinct hint
    /// (e.g. "device in use by another app") on top of the existing
    /// connection-state row.
    /// </summary>
    public StreamFailureReason Reason { get; }

    /// <summary>
    /// Gets the UTC timestamp of the state change.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <inheritdoc />
    public override string ToString()
        => $"PlayerStateChanged {{ {PreviousState} -> {NewState} }}";
}