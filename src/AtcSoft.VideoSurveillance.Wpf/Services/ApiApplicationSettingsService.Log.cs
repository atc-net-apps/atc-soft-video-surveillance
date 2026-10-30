namespace AtcSoft.VideoSurveillance.Wpf.Services;

public sealed partial class ApiApplicationSettingsService
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load settings from API server. Using defaults until reachable.")]
    private partial void LogLoadFromApiFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to read client preferences from {Path}; using defaults.")]
    private partial void LogClientPrefsReadFailed(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to write client preferences to {Path}.")]
    private partial void LogClientPrefsWriteFailed(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to push settings to API server.")]
    private partial void LogPushToApiFailed(Exception ex);
}