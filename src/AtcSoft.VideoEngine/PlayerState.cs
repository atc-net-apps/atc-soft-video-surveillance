namespace AtcSoft.VideoEngine;

/// <summary>
/// Specifies the state of a video player.
/// </summary>
public enum PlayerState
{
    /// <summary>
    /// The player is stopped and no stream is open.
    /// </summary>
    Stopped,

    /// <summary>
    /// The player is opening a stream.
    /// </summary>
    Opening,

    /// <summary>
    /// The player is actively playing a stream.
    /// </summary>
    Playing,

    /// <summary>
    /// The player encountered an error.
    /// </summary>
    Error,
}