namespace AtcSoft.VideoSurveillance.Wpf.App.Services;

/// <summary>
/// Builds and persists a client-side diagnostics report. Designed for
/// support flows: the operator hits "Export Diagnostics" in the
/// Backstage and emails / uploads the resulting JSON file.
/// </summary>
public interface IDiagnosticsExportService
{
    /// <summary>
    /// Collects the current diagnostic snapshot. Makes one gateway call
    /// (cameras list) — failures are captured in
    /// <see cref="DiagnosticsServerInfo.GatewayErrorMessage"/> rather
    /// than thrown, so a degraded-server scenario still produces a
    /// useful report.
    /// </summary>
    Task<DiagnosticsReport> BuildReportAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a built report to disk as pretty-printed JSON.
    /// </summary>
    Task WriteAsync(
        string filePath,
        DiagnosticsReport report,
        CancellationToken cancellationToken = default);
}