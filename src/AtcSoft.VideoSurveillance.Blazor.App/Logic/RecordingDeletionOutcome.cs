namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// How deleting a recording on the server went.
/// </summary>
public enum RecordingDeletionOutcome
{
    /// <summary>
    /// The recording is deleted.
    /// </summary>
    Deleted = 0,

    /// <summary>
    /// The recording is still being made.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// The server no longer has the recording.
    /// </summary>
    NotFound = 2,

    /// <summary>
    /// The server refused, or could not be reached.
    /// </summary>
    Failed = 3,
}