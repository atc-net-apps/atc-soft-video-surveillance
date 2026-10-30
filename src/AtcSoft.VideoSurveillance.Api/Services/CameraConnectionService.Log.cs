namespace AtcSoft.VideoSurveillance.Api.Services;

public sealed partial class CameraConnectionService
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Camera {CameraId} ({DisplayName}) - recording-on-connect disabled, skipping")]
    private partial void LogRecordingOnConnectDisabled(Guid cameraId, string displayName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dead recording pipeline detected for camera {CameraId} ({DisplayName}), cleaning up")]
    private partial void LogDeadPipelineDetected(Guid cameraId, string displayName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting recording-on-connect for camera {CameraId} ({DisplayName})")]
    private partial void LogStartingRecordingOnConnect(Guid cameraId, string displayName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to start recording for camera {CameraId} ({DisplayName}), will retry next iteration")]
    private partial void LogStartRecordingFailed(Exception ex, Guid cameraId, string displayName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error disposing pipeline for camera {CameraId}")]
    private partial void LogDisposePipelineError(Exception ex, Guid cameraId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping managed recording for camera {CameraId}")]
    private partial void LogStoppingManagedRecording(Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error stopping recording for camera {CameraId} during shutdown")]
    private partial void LogStopRecordingShutdownError(Exception ex, Guid cameraId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recording-on-connect started (fallback) for camera {CameraId} ({DisplayName})")]
    private partial void LogRecordingOnConnectStartedFallback(Guid cameraId, string displayName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to start recording (fallback) for camera {CameraId} ({DisplayName})")]
    private partial void LogStartRecordingFallbackFailed(Exception ex, Guid cameraId, string displayName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Camera {CameraId} ({DisplayName}) connected")]
    private partial void LogCameraConnected(Guid cameraId, string displayName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recording-on-connect started for camera {CameraId} ({DisplayName})")]
    private partial void LogRecordingOnConnectStarted(Guid cameraId, string displayName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to start recording after connect for camera {CameraId} ({DisplayName})")]
    private partial void LogStartRecordingAfterConnectFailed(Exception ex, Guid cameraId, string displayName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Camera {CameraId} ({DisplayName}) disconnected")]
    private partial void LogCameraDisconnected(Guid cameraId, string displayName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pipeline connection failed for camera {CameraId} ({DisplayName}), cleaning up")]
    private partial void LogPipelineConnectionFailed(Guid cameraId, string displayName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error disposing dead pipeline for camera {CameraId}")]
    private partial void LogDisposeDeadPipelineError(Exception ex, Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error disposing pipeline for camera {CameraId} (deferred)")]
    private partial void LogDisposePipelineDeferredError(Exception ex, Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Camera '{DisplayName}' ({CameraId}) failure #{ConsecutiveFailures}, backing off {BackoffSeconds}s (next attempt at {NextAttemptUtc:HH:mm:ss} UTC)")]
    private partial void LogBackoffScheduled(string displayName, Guid cameraId, int consecutiveFailures, int backoffSeconds, DateTime nextAttemptUtc);

    [LoggerMessage(Level = LogLevel.Information, Message = "Camera '{DisplayName}' ({CameraId}) reconnected after {ConsecutiveFailures} failure(s) (backoff cleared)")]
    private partial void LogReconnected(string displayName, Guid cameraId, int consecutiveFailures);

    [LoggerMessage(Level = LogLevel.Information, Message = "USB camera {CameraId} ('{FriendlyName}') unplugged — pipeline torn down, reconnect-backoff suspended")]
    private partial void LogUsbCameraUnplugged(Guid cameraId, string friendlyName);

    [LoggerMessage(Level = LogLevel.Information, Message = "USB camera {CameraId} ('{FriendlyName}') replugged — backoff cleared, will reconnect on next tick")]
    private partial void LogUsbCameraReplugged(Guid cameraId, string friendlyName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Opening camera {CameraId} ({CameraName}) to watch it for motion")]
    private partial void LogStartingMotionWatch(Guid cameraId, string cameraName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Motion detection could not be started for camera {CameraId} ({CameraName})")]
    private partial void LogStartMotionWatchFailed(Exception ex, Guid cameraId, string cameraName);

    [LoggerMessage(Level = LogLevel.Error, Message = "The motion recording of camera {CameraId} could not be started")]
    private partial void LogStartMotionRecordingFailed(Exception ex, Guid cameraId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Opening camera {CameraId} ({CameraName}) for {Use}")]
    private partial void LogOpeningForUse(Guid cameraId, string cameraName, CameraPipelineUse use);

    [LoggerMessage(Level = LogLevel.Error, Message = "Camera {CameraId} ({CameraName}) could not be opened for {Use}")]
    private partial void LogOpenForUseFailed(Exception ex, Guid cameraId, string cameraName, CameraPipelineUse use);

    [LoggerMessage(Level = LogLevel.Information, Message = "Closing camera {CameraId}: nothing uses it any more")]
    private partial void LogClosingUnusedCamera(Guid cameraId);
}