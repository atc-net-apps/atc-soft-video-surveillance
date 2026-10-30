namespace AtcSoft.VideoSurveillance.Wpf.App;

// MA0049 / CA1724 (type name matches namespace) suppressed via attributes on
// the App.xaml.cs partial declaration; one attribute covers the whole type.
public partial class App
{
    [LoggerMessage(Level = LogLevel.Error, Message = "CurrentDomain Unhandled Exception: {Message}")]
    private partial void LogCurrentDomainUnhandledException(string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "Dispatcher Unhandled Exception: {Message}")]
    private partial void LogDispatcherUnhandledException(string message);

    [LoggerMessage(Level = LogLevel.Information, Message = "App starting")]
    private partial void LogAppStarting();

    [LoggerMessage(Level = LogLevel.Warning, Message = "FFmpeg is not ready, the set-up dialog is shown: {Description}")]
    private partial void LogFFmpegNotReady(string description);

    [LoggerMessage(Level = LogLevel.Information, Message = "The server at '{Configured}' redirects to '{Resolved}'; the client uses the redirected address")]
    private partial void LogServerAddressRedirected(string configured, string resolved);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to read the settings from the server again")]
    private partial void LogSettingsReloadFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "App started")]
    private partial void LogAppStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "App closing")]
    private partial void LogAppClosing();

    [LoggerMessage(Level = LogLevel.Information, Message = "App closed")]
    private partial void LogAppClosed();
}