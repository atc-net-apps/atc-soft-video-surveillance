namespace AtcSoft.VideoSurveillance.Models.Settings;

/// <summary>
/// Settings for the recording playback overlay display.
/// </summary>
public class PlaybackOverlaySettings
{
    public bool ShowFilename { get; set; } = true;

    public string FilenameColor { get; set; } = "White";

    public bool ShowTimestamp { get; set; } = true;

    public string TimestampColor { get; set; } = "White";

    /// <inheritdoc />
    public override string ToString()
        => $"PlaybackOverlaySettings {{ Filename={ShowFilename}, Timestamp={ShowTimestamp} }}";
}