namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Service for automatically segmenting recordings at clock-aligned interval boundaries.
/// </summary>
public interface IRecordingSegmentationService
{
    event EventHandler<RecordingSegmentedEventArgs>? RecordingSegmented;

    bool IsRunning { get; }
}