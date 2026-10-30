namespace AtcSoft.VideoSurveillance.Api.Services;

public sealed partial class ServerRecordingSegmentationService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Server recording segmentation started (interval: {IntervalMinutes} minutes)")]
    private partial void LogSegmentationStarted(int intervalMinutes);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Interval boundary detected for camera {CameraId}, slot {Slot} (interval {IntervalMinutes} min)")]
    private partial void LogIntervalBoundaryDetected(Guid cameraId, int slot, int intervalMinutes);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Max duration reached for camera {CameraId}, duration {Duration}")]
    private partial void LogMaxDurationReached(Guid cameraId, TimeSpan duration);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to segment recording for camera {CameraId}")]
    private partial void LogSegmentingFailed(Guid cameraId);
}