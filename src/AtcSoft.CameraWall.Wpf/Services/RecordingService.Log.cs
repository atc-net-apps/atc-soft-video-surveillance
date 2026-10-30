namespace AtcSoft.CameraWall.Wpf.Services;

public partial class RecordingService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Recording started for camera: '{CameraName}', file: {FilePath}")]
    private partial void LogRecordingStarted(string cameraName, string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start recording for camera: {CameraName}")]
    private partial void LogRecordingStartFailed(Exception ex, string cameraName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recording stopped for camera '{CameraName}', file: {FilePath}")]
    private partial void LogRecordingStopped(string cameraName, string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error stopping recording for camera '{CameraName}'")]
    private partial void LogRecordingStopError(Exception ex, string cameraName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Motion recording started for camera: {CameraName}, file: {FilePath}")]
    private partial void LogMotionRecordingStarted(string cameraName, string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start motion recording for camera: {CameraName}")]
    private partial void LogMotionRecordingStartFailed(Exception ex, string cameraName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping all recordings ({Count} active sessions)")]
    private partial void LogStoppingAllRecordings(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No active session for camera ID: {CameraId}, cannot segment")]
    private partial void LogNoActiveSessionForSegment(Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No pipeline found for camera '{CameraName}', cannot segment")]
    private partial void LogNoPipelineForSegment(string cameraName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Camera not found for ID: {CameraId}, cannot segment")]
    private partial void LogCameraNotFoundForSegment(Guid cameraId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Segmenting recording for camera: {CameraName}, old file: {OldFilePath}")]
    private partial void LogSegmentingRecording(string cameraName, string oldFilePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Segment swap lost the race for camera '{CameraName}' — StopRecording ran concurrently; segment abandoned")]
    private partial void LogSegmentRaceLost(string cameraName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recording segmented for camera: {CameraName}, new file: {NewFilePath}")]
    private partial void LogRecordingSegmented(string cameraName, string newFilePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to segment recording for camera: {CameraName}")]
    private partial void LogSegmentRecordingFailed(Exception ex, string cameraName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Post-motion period elapsed for camera '{CameraName}', stopping recording")]
    private partial void LogPostMotionPeriodElapsed(string cameraName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Transitioning to post-motion state for camera '{CameraName}', {Remaining}s remaining")]
    private partial void LogTransitioningToPostMotion(string cameraName, string remaining);

    [LoggerMessage(Level = LogLevel.Error, Message = "Post-motion tick failed for camera '{CameraName}'; will retry on next interval")]
    private partial void LogPostMotionTickFailed(Exception ex, string cameraName);

    [LoggerMessage(Level = LogLevel.Error, Message = "RecordingStateChanged subscriber threw for camera '{CameraName}'; continuing to next subscriber")]
    private partial void LogRecordingStateChangedSubscriberFailed(Exception ex, string cameraName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The disk-space guard failed; new recordings are refused until it works")]
    private partial void LogDiskSpaceReclaimFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Disk still below the space kept free mid-recording: stopping {Count} recording(s) on that volume")]
    private partial void LogDiskSpaceGuardStoppingRecordings(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Recording of camera '{CameraName}' stopped: nothing more can be written ({Error})")]
    private partial void LogRecordingWriteFailed(string cameraName, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Not enough free space where camera '{CameraName}' records, or it cannot be read: the recording is not started")]
    private partial void LogDiskSpaceGateFailed(string cameraName);
}