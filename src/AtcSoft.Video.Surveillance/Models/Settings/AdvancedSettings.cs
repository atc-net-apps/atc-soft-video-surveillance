namespace AtcSoft.Video.Surveillance.Models.Settings;

/// <summary>
/// Application-level advanced settings for debugging and logging.
/// </summary>
public class AdvancedSettings
{
    public bool EnableDebugLogging { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the app records diagnostics for a long test run:
    /// trace-level log files kept 31 days, a report of the system and its settings at start-up, and a
    /// health sample (memory, CPU, recordings, disks, the UI thread) every minute. Takes effect at the next start.
    /// </summary>
    public bool EnableDiagnostics { get; set; }

    public string LogPath { get; set; } = ApplicationPaths.DefaultLogsPath;

    /// <inheritdoc />
    public override string ToString()
        => $"AdvancedSettings {{ DebugLogging={EnableDebugLogging}, Diagnostics={EnableDiagnostics} }}";
}