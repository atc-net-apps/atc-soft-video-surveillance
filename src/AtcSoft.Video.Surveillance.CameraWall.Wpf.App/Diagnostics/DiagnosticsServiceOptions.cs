namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.App.Diagnostics;

/// <summary>
/// The options of <see cref="DiagnosticsService"/>: a health sample a minute while diagnostics are on;
/// with them off, the worker parks on a long delay and does nothing.
/// </summary>
public sealed class DiagnosticsServiceOptions : IBackgroundServiceOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether diagnostics are on (<c>Advanced.EnableDiagnostics</c> at start-up).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the folder the report and the samples are written to.
    /// </summary>
    public string LogPath { get; set; } = string.Empty;

    /// <inheritdoc />
    public ushort StartupDelaySeconds { get; set; } = 15;

    /// <inheritdoc />
    public ushort RepeatIntervalSeconds { get; set; } = 60;

    /// <inheritdoc />
    public string? ServiceName { get; set; } = nameof(DiagnosticsService);
}