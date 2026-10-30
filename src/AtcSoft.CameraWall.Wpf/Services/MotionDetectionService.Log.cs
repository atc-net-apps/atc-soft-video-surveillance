namespace AtcSoft.CameraWall.Wpf.Services;

/// <summary>
/// Source-generated high-performance log methods for <see cref="MotionDetectionService"/>.
/// </summary>
public partial class MotionDetectionService
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Motion frame capture failed for camera {CameraId} (consecutive failure #{ConsecutiveFails})")]
    private partial void LogFrameCaptureFailed(Exception ex, Guid cameraId, int consecutiveFails);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Motion frame analysis failed for camera {CameraId}")]
    private partial void LogFrameAnalysisFailed(Exception ex, Guid cameraId);
}