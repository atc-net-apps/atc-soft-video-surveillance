namespace AtcSoft.VideoSurveillance.Wpf.App.Services;

public partial class DiagnosticsExportService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Diagnostics report written to {FilePath}")]
    private partial void LogDiagnosticsExported(string filePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gateway probe for diagnostics report failed")]
    private partial void LogGatewayProbeFailed(Exception ex);
}