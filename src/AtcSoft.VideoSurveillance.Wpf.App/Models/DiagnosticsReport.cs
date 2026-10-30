namespace AtcSoft.VideoSurveillance.Wpf.App.Models;

/// <summary>
/// Snapshot of client-side diagnostic info — what the user sees from
/// their WPF client at a single point in time. Serialized to JSON so
/// support can read it without running anything. Deliberately does not
/// include log file contents: logs can be large, contain rolling-locks,
/// and would inflate the report from KB to MB without adding much that
/// the log path field alone doesn't tell support how to retrieve.
/// </summary>
public sealed class DiagnosticsReport
{
    public DateTimeOffset GeneratedAt { get; set; }

    public DiagnosticsClientInfo Client { get; set; } = new();

    public DiagnosticsServerInfo Server { get; set; } = new();

    public IReadOnlyList<DiagnosticsCameraInfo> Cameras { get; set; } = [];
}