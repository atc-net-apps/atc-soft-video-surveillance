namespace AtcSoft.Video.Surveillance.Api.Services;

public sealed partial class JsonApplicationSettingsService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded application settings from {Path}")]
    private partial void LogSettingsLoaded(string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created default settings file at {Path}")]
    private partial void LogDefaultSettingsCreated(string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to create default settings file at {Path}")]
    private partial void LogDefaultSettingsCreateFailed(Exception ex, string path);
}