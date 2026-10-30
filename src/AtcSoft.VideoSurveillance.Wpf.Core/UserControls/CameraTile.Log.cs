namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

/// <summary>
/// Source-generated high-performance log methods for <see cref="CameraTile"/>.
/// </summary>
public partial class CameraTile
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Recording-on-connect starting for camera '{CameraName}'")]
    private partial void LogRecordingOnConnectStarting(string cameraName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recording-on-connect enabled but skipped for camera '{CameraName}' (pipeline={HasPipeline}, service={HasService}, state={RecordingState})")]
    private partial void LogRecordingOnConnectSkipped(string cameraName, bool hasPipeline, bool hasService, RecordingState recordingState);

    [LoggerMessage(Level = LogLevel.Information, Message = "Auto-reconnect scheduled for camera '{CameraName}' in {DelaySeconds}s")]
    private partial void LogAutoReconnectScheduled(string cameraName, int delaySeconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Auto-reconnect attempting for camera '{CameraName}'")]
    private partial void LogAutoReconnectAttempting(string cameraName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stream stale detected for camera '{CameraName}' (no frames for 15s), triggering reconnect")]
    private partial void LogStreamStaleDetected(string cameraName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection timeout for camera '{CameraName}' after {Seconds}s")]
    private partial void LogConnectionTimeout(string cameraName, int seconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tile loaded: '{CameraName}' (window={WindowState}, isAncestor={WindowIsAncestor})")]
    private partial void LogTileLoaded(string cameraName, string windowState, bool windowIsAncestor);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tile unloaded: '{CameraName}' (state={ConnectionState}, isLoaded={IsLoaded}, window={WindowState}, isAncestor={WindowIsAncestor}, parent={ParentType})")]
    private partial void LogTileUnloaded(string cameraName, string connectionState, bool isLoaded, string windowState, bool windowIsAncestor, string parentType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tile disposing: '{CameraName}' (state={ConnectionState})")]
    private partial void LogTileDispose(string cameraName, string connectionState);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reconnecting '{CameraName}' on tile reload (state was {PreviousState}) — recovering from a transient WPF unload")]
    private partial void LogReconnectingAfterReload(string cameraName, string previousState);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Motion boxes for camera '{CameraName}' not drawn: the tile has no motion overlay yet")]
    private partial void LogMotionOverlayNotFound(string cameraName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Snapshot capture or save failed")]
    private partial void LogSnapshotFailed(Exception ex);
}