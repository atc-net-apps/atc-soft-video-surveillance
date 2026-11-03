namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

/// <summary>
/// Closing CameraWall stops its recording: what records now, and what waits for motion (or a
/// connection) to start recording. So the user is asked first while any camera records or is set
/// to record, with how many of the cameras that is. With recording off for every camera (only
/// watching) it closes without a question.
/// </summary>
public static class CloseConfirmation
{
    /// <summary>
    /// Gets how many cameras record now, and how many record now or are set to record (on motion
    /// or on connect, the camera's own setting or the app's).
    /// </summary>
    /// <param name="cameras">The cameras.</param>
    /// <param name="recordingNow">The cameras recording now (on demand, on connect, on motion).</param>
    /// <param name="recording">The app's recording settings.</param>
    /// <returns>The cameras recording now, and those recording or set to record.</returns>
    public static (int RecordingNow, int Active) Count(
        IReadOnlyCollection<AtcSoft.Video.Surveillance.Wpf.Core.Models.CameraConfiguration> cameras,
        IReadOnlyCollection<Guid> recordingNow,
        RecordingSettings recording)
    {
        ArgumentNullException.ThrowIfNull(cameras);
        ArgumentNullException.ThrowIfNull(recordingNow);
        ArgumentNullException.ThrowIfNull(recording);

        // The camera's own setting, else the app's (as MotionDetectionPolicy and RecordingPolicyHelper).
        var now = cameras.Count(c => recordingNow.Contains(c.Id));
        var active = cameras.Count(c =>
            recordingNow.Contains(c.Id) ||
            (c.Overrides?.Recording.EnableRecordingOnMotion ?? recording.EnableRecordingOnMotion) ||
            (c.Overrides?.Recording.EnableRecordingOnConnect ?? recording.EnableRecordingOnConnect));
        return (now, active);
    }

    /// <summary>
    /// Gets the question to ask before closing; <see langword="null"/> when no camera records or is
    /// set to record.
    /// </summary>
    /// <param name="cameras">How many cameras there are.</param>
    /// <param name="active">How many record now or are set to record.</param>
    /// <param name="recordingNow">How many record now.</param>
    public static string? Question(
        int cameras,
        int active,
        int recordingNow)
        => active > 0
            ? string.Format(CultureInfo.CurrentCulture, Translations.CloseWhileRecording3, active, Math.Max(cameras, active), recordingNow)
            : null;
}