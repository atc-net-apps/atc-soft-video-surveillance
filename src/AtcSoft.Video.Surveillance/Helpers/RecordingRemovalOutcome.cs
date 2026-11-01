namespace AtcSoft.Video.Surveillance.Helpers;

/// <summary>
/// How removing a recording went.
/// </summary>
public enum RecordingRemovalOutcome
{
    /// <summary>
    /// The recording and its thumbnail are gone.
    /// </summary>
    Removed = 0,

    /// <summary>
    /// There is no such recording.
    /// </summary>
    NotFound = 1,

    /// <summary>
    /// The recording is still being made.
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// The path does not name a recording below the recording folder.
    /// </summary>
    NotARecording = 3,

    /// <summary>
    /// The file may not be deleted: it is read-only, or the account the
    /// program runs as may not delete it.
    /// </summary>
    Refused = 4,
}