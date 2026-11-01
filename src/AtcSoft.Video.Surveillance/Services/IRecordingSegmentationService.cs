namespace AtcSoft.Video.Surveillance.Services;

/// <summary>
/// Service for automatically segmenting recordings at clock-aligned interval boundaries.
/// </summary>
public interface IRecordingSegmentationService
{
    event EventHandler<RecordingSegmentedEventArgs>? RecordingSegmented;

    bool IsRunning { get; }

    /// <summary>
    /// Gets when the worker last finished a pass (segments, disk space guard, restarting recordings),
    /// or <see langword="null"/> before the first. A pass that is long ago means the worker is stuck.
    /// </summary>
    DateTime? LastPassUtc => null;
}