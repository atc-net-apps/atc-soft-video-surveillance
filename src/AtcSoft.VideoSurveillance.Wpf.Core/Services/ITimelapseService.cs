namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// Service for managing timelapse capture sessions.
/// </summary>
public interface ITimelapseService
{
    /// <summary>
    /// Occurs when a timelapse frame is captured.
    /// </summary>
    event EventHandler<TimelapseFrameCapturedEventArgs>? FrameCaptured;

    /// <summary>
    /// Starts timelapse capture for a camera.
    /// </summary>
    /// <param name="camera">The camera configuration.</param>
    /// <param name="pipeline">The media pipeline instance.</param>
    void StartCapture(
        CameraConfiguration camera,
        IMediaPipeline pipeline);

    /// <summary>
    /// Stops timelapse capture for a camera.
    /// </summary>
    /// <param name="cameraId">The camera ID.</param>
    void StopCapture(Guid cameraId);

    /// <summary>
    /// Stops all active timelapse capture sessions.
    /// </summary>
    void StopAllCaptures();

    /// <summary>
    /// Checks if timelapse capture is active for a camera.
    /// </summary>
    /// <param name="cameraId">The camera ID.</param>
    /// <returns>True if capturing; otherwise, false.</returns>
    bool IsCapturing(Guid cameraId);

    /// <summary>
    /// Gets the effective timelapse interval for a camera, considering override settings.
    /// </summary>
    /// <param name="camera">The camera configuration.</param>
    /// <returns>The effective interval as a TimeSpan.</returns>
    TimeSpan GetEffectiveInterval(CameraConfiguration camera);

    /// <summary>
    /// Gets the effective timelapse enabled state for a camera, considering override settings.
    /// </summary>
    /// <param name="camera">The camera configuration.</param>
    /// <returns>True if timelapse is enabled; otherwise, false.</returns>
    bool GetEffectiveEnabled(CameraConfiguration camera);
}