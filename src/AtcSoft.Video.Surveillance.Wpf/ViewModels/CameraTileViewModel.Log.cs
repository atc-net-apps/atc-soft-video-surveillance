namespace AtcSoft.Video.Surveillance.Wpf.ViewModels;

public sealed partial class CameraTileViewModel
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Live tile for camera {CameraId}: the player failed ({Reason}); the stream starts again")]
    private partial void LogPlayerFailed(Guid cameraId, string? reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Live tile for camera {CameraId}: the player could not open the stream; it tries again")]
    private partial void LogPlayerOpenFailed(Exception ex, Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Live tile for camera {CameraId}: the stream could not be started; it tries again")]
    private partial void LogStreamStartFailed(Exception ex, Guid cameraId);
}