namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// A recording service that implements only what the interface demands.
/// </summary>
public abstract class RecordingServiceWithoutSharedPipelines : IRecordingService
{
    public abstract event EventHandler<RecordingStateChangedEventArgs>? RecordingStateChanged;

    public abstract RecordingState GetRecordingState(Guid cameraId);

    public abstract RecordingSession? GetSession(Guid cameraId);

    public abstract IMediaPipeline? FindPipeline(Guid cameraId);

    public abstract bool StartRecording(
        CameraConfiguration camera,
        IMediaPipeline pipeline);

    public abstract void StopRecording(Guid cameraId);

    public abstract void StopAllRecordings();

    public abstract bool IsRecording(Guid cameraId);

    public abstract bool TriggerMotionRecording(
        CameraConfiguration camera,
        IMediaPipeline pipeline);

    public abstract void UpdateMotionTimestamp(Guid cameraId);

    public abstract bool SegmentRecording(Guid cameraId);

    public abstract IReadOnlyList<RecordingSession> GetActiveSessions();

    public abstract int ReapInactiveSessions();

    public abstract string GenerateRecordingFilename(
        CameraConfiguration camera,
        string format);

    public abstract bool EnforceDiskSpaceGuard();
}