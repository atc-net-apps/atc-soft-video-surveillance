namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// What the recordings page says before and after it deletes a recording.
/// </summary>
public static class RecordingDeletion
{
    /// <summary>
    /// Gets the question asked before a recording is deleted.
    /// </summary>
    /// <param name="cameraName">The camera, if it is still known.</param>
    /// <param name="started">When the recording started, as the list shows it.</param>
    public static string Question(
        string? cameraName,
        string started)
        => string.IsNullOrEmpty(cameraName)
            ? string.Format(CultureInfo.CurrentCulture, Translations.DeleteRecordingQuestion1, started)
            : string.Format(CultureInfo.CurrentCulture, Translations.DeleteCameraRecordingQuestion2, cameraName, started);

    /// <summary>
    /// Gets what to tell the user about the outcome.
    /// </summary>
    public static string ResultText(RecordingDeletionOutcome outcome)
        => outcome switch
        {
            RecordingDeletionOutcome.Deleted => Translations.RecordingDeleted,
            RecordingDeletionOutcome.InProgress => Translations.RecordingStillBeingMade,
            RecordingDeletionOutcome.NotFound => Translations.RecordingAlreadyGone,
            _ => Translations.RecordingCouldNotBeDeleted,
        };

    /// <summary>
    /// Gets whether the outcome is the one the user asked for.
    /// </summary>
    public static bool Succeeded(RecordingDeletionOutcome outcome)
        => outcome == RecordingDeletionOutcome.Deleted;

    /// <summary>
    /// Gets whether the recording no longer exists and leaves the list.
    /// </summary>
    public static bool IsGone(RecordingDeletionOutcome outcome)
        => outcome is RecordingDeletionOutcome.Deleted or RecordingDeletionOutcome.NotFound;
}