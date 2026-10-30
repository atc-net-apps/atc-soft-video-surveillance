namespace AtcSoft.VideoSurveillance.Wpf.App.Services;

public partial class AutoStartService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Auto-start enabled (path={ExePath})")]
    private partial void LogAutoStartEnabled(string exePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Auto-start disabled")]
    private partial void LogAutoStartDisabled();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Auto-start read failed; treating as disabled")]
    private partial void LogAutoStartReadFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Auto-start enable failed")]
    private partial void LogAutoStartEnableFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Auto-start disable failed")]
    private partial void LogAutoStartDisableFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Auto-start cannot resolve executable path (Environment.ProcessPath is null/empty)")]
    private partial void LogAutoStartExecutablePathUnavailable();
}