namespace AtcSoft.VideoSurveillance.Models.Settings;

/// <summary>
/// Application-level advanced settings for debugging and logging.
/// </summary>
public class AdvancedSettings
{
    public bool EnableDebugLogging { get; set; }

    public string LogPath { get; set; } = ApplicationPaths.DefaultLogsPath;

    /// <inheritdoc />
    public override string ToString()
        => $"AdvancedSettings {{ DebugLogging={EnableDebugLogging} }}";
}