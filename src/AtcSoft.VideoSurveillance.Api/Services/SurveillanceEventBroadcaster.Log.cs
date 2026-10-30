namespace AtcSoft.VideoSurveillance.Api.Services;

public sealed partial class SurveillanceEventBroadcaster
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Surveillance event broadcaster started")]
    private partial void LogBroadcasterStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "Surveillance event broadcaster stopped")]
    private partial void LogBroadcasterStopped();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to broadcast RecordingStateChanged for camera {CameraId}")]
    private partial void LogBroadcastRecordingFailed(Exception ex, Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to broadcast MotionDetected for camera {CameraId}")]
    private partial void LogBroadcastMotionFailed(Exception ex, Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to broadcast ConnectionStateChanged for camera {CameraId}")]
    private partial void LogBroadcastConnectionFailed(Exception ex, Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to broadcast UsbCameraLifecycleChanged for camera {CameraId}")]
    private partial void LogBroadcastUsbLifecycleFailed(Exception ex, Guid cameraId);
}