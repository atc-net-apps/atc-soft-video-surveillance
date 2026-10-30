namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Service for managing timelapse capture sessions.
/// </summary>
public interface ITimelapseService
{
    event EventHandler<TimelapseFrameCapturedEventArgs>? FrameCaptured;

    void StartCapture(
        CameraConfiguration camera,
        IMediaPipeline pipeline);

    void StopCapture(Guid cameraId);

    void StopAllCaptures();

    bool IsCapturing(Guid cameraId);

    TimeSpan GetEffectiveInterval(CameraConfiguration camera);

    bool GetEffectiveEnabled(CameraConfiguration camera);
}