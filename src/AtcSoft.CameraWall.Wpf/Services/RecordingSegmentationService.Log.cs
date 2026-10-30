namespace AtcSoft.CameraWall.Wpf.Services;

public partial class RecordingSegmentationService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Recording segmentation service started")]
    private partial void LogSegmentationStarted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Interval boundary detected for camera '{CameraName}', slot: {CurrentSlot} (interval: {Interval} min)")]
    private partial void LogIntervalBoundaryDetected(string cameraName, int currentSlot, int interval);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Max duration reached for camera '{CameraName}', duration: {Duration}")]
    private partial void LogMaxDurationReached(string cameraName, TimeSpan duration);

    [LoggerMessage(Level = LogLevel.Information, Message = "Segmenting recording for camera '{CameraName}', reason: {Reason}")]
    private partial void LogSegmentingRecording(string cameraName, SegmentationReason reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to segment recording for camera '{CameraName}'")]
    private partial void LogSegmentingFailed(string cameraName);
}