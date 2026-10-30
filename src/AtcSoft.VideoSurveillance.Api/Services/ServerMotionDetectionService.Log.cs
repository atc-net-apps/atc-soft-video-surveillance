namespace AtcSoft.VideoSurveillance.Api.Services;

public sealed partial class ServerMotionDetectionService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Motion detection started for camera {CameraId}")]
    private partial void LogMotionDetectionStarted(Guid cameraId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Motion detection stopped for camera {CameraId}")]
    private partial void LogMotionDetectionStopped(Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Motion detection loop error for camera {CameraId}")]
    private partial void LogMotionDetectionLoopError(Exception ex, Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Motion detection is not available on this host (no picture reader); camera {CameraId} is not watched for motion")]
    private partial void LogMotionDetectionNotAvailable(Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No picture for motion detection from camera {CameraId} ({ConsecutiveFailures} in a row)")]
    private partial void LogFrameCaptureFailed(Exception ex, Guid cameraId, int consecutiveFailures);

    [LoggerMessage(Level = LogLevel.Information, Message = "The pipeline of camera {CameraId} was closed; its motion detection ends")]
    private partial void LogPipelineClosed(Guid cameraId);

    [LoggerMessage(Level = LogLevel.Error, Message = "A subscriber to motion events failed for camera {CameraId}")]
    private partial void LogMotionSubscriberFailed(Exception ex, Guid cameraId);
}