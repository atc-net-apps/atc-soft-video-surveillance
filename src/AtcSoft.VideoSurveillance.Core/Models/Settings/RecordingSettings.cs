namespace AtcSoft.VideoSurveillance.Models.Settings;

/// <summary>
/// Application-level recording settings.
/// </summary>
public class RecordingSettings
{
    public string RecordingPath { get; set; } = ApplicationPaths.DefaultRecordingsPath;

    public string RecordingFormat { get; set; } = "mkv";

    public VideoTranscodeCodec TranscodeVideoCodec { get; set; }

    public bool EnableRecordingOnMotion { get; set; }

    public bool EnableRecordingOnConnect { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an activity index is kept beside every recording
    /// (<c>{video}.activity.jsonl</c>), for the timelines. It costs the analysis of the recorded
    /// cameras; turned off, the next recording or segment is made without one.
    /// </summary>
    public bool EnableActivityIndex { get; set; } = true;

    public MediaCleanupSettings Cleanup { get; set; } = new();

    public PlaybackOverlaySettings PlaybackOverlay { get; set; } = new();

    public bool EnableHourlySegmentation { get; set; } = true;

    public int MaxRecordingDurationMinutes { get; set; } = 60;

    public int ThumbnailTileCount { get; set; } = 4;

    /// <summary>
    /// Gets or sets a value indicating whether recordings go into a folder
    /// per day (<c>yyyy-MM-dd</c>) below the camera folder. On by default.
    /// </summary>
    public bool UseDailySubfolders { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether thumbnails are kept in a
    /// <c>thumbnails</c> folder below the recording path instead of next to
    /// the videos. On by default.
    /// </summary>
    public bool SeparateThumbnailFolder { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the recordings browser closes
    /// when a recording is played. Off by default: the browser stays open
    /// behind the player.
    /// </summary>
    public bool CloseRecordingsBrowserOnPlay { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the recordings browser opens
    /// with the filters and the selection it was last closed with. Kept
    /// while the app runs. On by default.
    /// </summary>
    public bool RememberRecordingsBrowserState { get; set; } = true;

    public bool EnableTimelapse { get; set; }

    public string TimelapseInterval { get; set; } = "5m";

    /// <inheritdoc />
    public override string ToString()
        => $"RecordingSettings {{ Path='{RecordingPath}', Format='{RecordingFormat}' }}";
}