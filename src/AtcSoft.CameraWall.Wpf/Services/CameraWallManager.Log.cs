namespace AtcSoft.CameraWall.Wpf.Services;

public sealed partial class CameraWallManager
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Camera connection state changed: '{CameraName}' - '{OldState}' -> '{NewState}'")]
    private partial void LogCameraConnectionStateChanged(string cameraName, string oldState, string newState);

    [LoggerMessage(Level = LogLevel.Information, Message = "Camera '{CameraName}' connected - RecordOnConnect: {RecordOnConnect}")]
    private partial void LogCameraConnectedRecordOnConnect(string cameraName, bool recordOnConnect);

    [LoggerMessage(Level = LogLevel.Information, Message = "Update available: current {CurrentVersion}, latest {LatestVersion}")]
    private partial void LogUpdateAvailable(string currentVersion, string latestVersion);

    [LoggerMessage(Level = LogLevel.Information, Message = "Layout changed to: '{LayoutName}' with {CameraCount} cameras")]
    private partial void LogLayoutChanged(string layoutName, int cameraCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Camera: '{CameraName}' - RecordOnConnect: {RecordOnConnect}, RecordOnMotion: {RecordOnMotion}")]
    private partial void LogCameraRecordingSettings(string cameraName, bool recordOnConnect, bool recordOnMotion);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading startup layout: '{LayoutName}' with {CameraCount} cameras")]
    private partial void LogLoadingStartupLayout(string layoutName, int cameraCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "No startup layout configured, using first layout: '{LayoutName}'")]
    private partial void LogNoStartupLayoutUsingFirst(string layoutName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recording settings - EnableRecordingOnConnect: {RecordOnConnect}, EnableRecordingOnMotion: {RecordOnMotion}")]
    private partial void LogRecordingSettings(bool recordOnConnect, bool recordOnMotion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Session switch detected ({Reason}) — recreating connected players to rebuild GPU after desktop session change")]
    private partial void LogSessionSwitchReconnect(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Detected connection type {ConnectionType} for camera '{DisplayName}'")]
    private partial void LogConnectionTypeDetected(string displayName, string connectionType);
}