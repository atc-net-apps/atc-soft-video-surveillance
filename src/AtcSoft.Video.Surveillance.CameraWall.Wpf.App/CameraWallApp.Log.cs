namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.App;

public partial class CameraWallApp
{
    [LoggerMessage(Level = LogLevel.Information, Message = "App initializing")]
    private partial void LogAppInitializing();

    [LoggerMessage(Level = LogLevel.Error, Message = "CurrentDomain Unhandled Exception: {ExceptionMessage}")]
    private partial void LogCurrentDomainUnhandledException(Exception ex, string exceptionMessage);

    [LoggerMessage(Level = LogLevel.Error, Message = "Dispatcher Unhandled Exception: {ExceptionMessage}")]
    private partial void LogDispatcherUnhandledException(Exception ex, string exceptionMessage);

    [LoggerMessage(Level = LogLevel.Critical, Message = "{Count} errors in the UI within {Seconds:0} s: CameraWall closes and starts again")]
    private partial void LogTooManyErrorsRestarting(int count, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The background services did not stop within 15 s; CameraWall closes anyway")]
    private partial void LogHostStopTimedOut();

    [LoggerMessage(Level = LogLevel.Error, Message = "CameraWall could not start again")]
    private partial void LogRestartFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A task failed and nobody looked at it")]
    private partial void LogUnobservedTaskException(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "App starting")]
    private partial void LogAppStarting();

    [LoggerMessage(Level = LogLevel.Information, Message = "App started")]
    private partial void LogAppStarted();

    [LoggerMessage(Level = LogLevel.Warning, Message = "FFmpeg is not ready, the set-up dialog is shown: {Description}")]
    private partial void LogFFmpegNotReady(string description);

    [LoggerMessage(Level = LogLevel.Information, Message = "App closing")]
    private partial void LogAppClosing();

    [LoggerMessage(Level = LogLevel.Information, Message = "All recordings stopped")]
    private partial void LogAllRecordingsStopped();

    [LoggerMessage(Level = LogLevel.Information, Message = "App closed")]
    private partial void LogAppClosed();
}