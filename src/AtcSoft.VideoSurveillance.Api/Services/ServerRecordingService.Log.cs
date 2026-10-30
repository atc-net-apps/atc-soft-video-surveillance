namespace AtcSoft.VideoSurveillance.Api.Services;

public sealed partial class ServerRecordingService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Recording started for camera '{CameraName}' ({CameraId}): {FilePath}")]
    private partial void LogRecordingStarted(string cameraName, Guid cameraId, string filePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recording stopped for camera '{CameraName}' ({CameraId})")]
    private partial void LogRecordingStopped(string cameraName, Guid cameraId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recording segmented for camera '{CameraName}' ({CameraId}): {FilePath}")]
    private partial void LogSegmented(string cameraName, Guid cameraId, string filePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cannot segment camera {CameraId}: pipeline reference missing")]
    private partial void LogSegmentNoPipeline(Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cannot segment camera {CameraId}: camera not found in storage")]
    private partial void LogSegmentCameraNotFound(Guid cameraId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Segmentation failed for camera {CameraId}")]
    private partial void LogSegmentFailed(Exception ex, Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stopping pipeline for camera {CameraId} threw")]
    private partial void LogStopPipelineFailed(Exception ex, Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Disposing pipeline for camera {CameraId} threw")]
    private partial void LogDisposePipelineFailed(Exception ex, Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reaping inactive recording session for camera '{CameraName}' ({CameraId}): {Reason}")]
    private partial void LogReapingInactiveSession(string cameraName, Guid cameraId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reaper swept {ReapedCount} inactive recording session(s)")]
    private partial void LogReaperSwept(int reapedCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Disk space reclaim failed before recording start")]
    private partial void LogDiskSpaceReclaimFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Disk still below the space kept free mid-recording: stopping {Count} recording(s)")]
    private partial void LogDiskSpaceGuardStoppingRecordings(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Recording of camera '{CameraName}' stopped: nothing more can be written ({Error})")]
    private partial void LogRecordingWriteFailed(string cameraName, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Disk space still below threshold after reclaim — recording aborted for camera '{CameraName}'")]
    private partial void LogDiskSpaceGateFailed(string cameraName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Thumbnail capture failed for camera {CameraId}; the recording is not affected")]
    private partial void LogThumbnailFailed(Exception ex, Guid cameraId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Camera '{CameraName}' has a recording folder of its own, '{OwnFolder}'; the server records every camera below '{RecordingPath}', which it lists, serves and cleans")]
    private partial void LogOwnRecordingFolderNotUsed(string cameraName, string ownFolder, string recordingPath);
}