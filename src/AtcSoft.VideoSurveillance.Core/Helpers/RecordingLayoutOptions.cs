namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// How new recordings are laid out below the recording root. It only
/// affects where files are written; reading handles every layout.
/// </summary>
/// <param name="UseDailySubfolders">Put each day's recordings in their own <c>yyyy-MM-dd</c> folder.</param>
/// <param name="SeparateThumbnailFolder">Keep thumbnails in a <c>thumbnails</c> folder below the root instead of next to the videos.</param>
public sealed record RecordingLayoutOptions(
    bool UseDailySubfolders,
    bool SeparateThumbnailFolder)
{
    /// <summary>
    /// The original layout: one folder per camera holding videos and thumbnails.
    /// </summary>
    public static RecordingLayoutOptions Flat { get; } = new(UseDailySubfolders: false, SeparateThumbnailFolder: false);

    /// <summary>
    /// Creates the layout options from the recording settings.
    /// </summary>
    /// <param name="settings">The recording settings.</param>
    public static RecordingLayoutOptions From(RecordingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new RecordingLayoutOptions(settings.UseDailySubfolders, settings.SeparateThumbnailFolder);
    }
}