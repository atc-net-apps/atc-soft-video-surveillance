namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

/// <summary>
/// Closing CameraWall stops its recordings, so the user is asked first while any camera records.
/// With nothing recording (only watching) it closes without a question.
/// </summary>
public static class CloseConfirmation
{
    /// <summary>
    /// Gets the question to ask before closing; <see langword="null"/> when nothing records.
    /// </summary>
    /// <param name="activeRecordings">The recordings running now (on demand, on connect, on motion).</param>
    public static string? Question(int activeRecordings)
        => activeRecordings > 0
            ? string.Format(CultureInfo.CurrentCulture, Translations.CloseWhileRecording1, activeRecordings)
            : null;
}