namespace AtcSoft.CameraWall.Wpf.Services;

public partial class TimelapseService
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Timelapse not enabled for camera: {CameraName}")]
    private partial void LogTimelapseNotEnabled(string cameraName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to add timelapse context for camera: {CameraName}")]
    private partial void LogFailedToAddTimelapseContext(string cameraName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Timelapse capture started for camera: {CameraName}, interval: {Interval}")]
    private partial void LogTimelapseCaptureStarted(string cameraName, TimeSpan interval);

    [LoggerMessage(Level = LogLevel.Information, Message = "Timelapse capture stopped for camera: {CameraName}")]
    private partial void LogTimelapseCaptureStopped(string cameraName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopping all timelapse captures ({Count} active)")]
    private partial void LogStoppingAllTimelapseCaptures(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Timelapse snapshot returned no data for camera: {CameraName}")]
    private partial void LogTimelapseSnapshotNoData(string cameraName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Timelapse frame captured for camera: {CameraName}, file: {FilePath}")]
    private partial void LogTimelapseFrameCaptured(string cameraName, string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to capture timelapse frame for camera: {CameraName}")]
    private partial void LogTimelapseFrameCaptureFailed(Exception ex, string cameraName);
}