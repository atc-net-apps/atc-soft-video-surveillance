namespace AtcSoft.VideoSurveillance.Events;

/// <summary>
/// One picture of a camera was analysed: what the detector found, whether or not it is motion.
/// </summary>
/// <param name="cameraId">The camera.</param>
/// <param name="time">When the picture was taken.</param>
/// <param name="result">What the detector found in it.</param>
public sealed class PictureAnalyzedEventArgs(
    Guid cameraId,
    DateTimeOffset time,
    MotionDetectorResult result) : EventArgs
{
    /// <summary>
    /// Gets the camera.
    /// </summary>
    public Guid CameraId { get; } = cameraId;

    /// <summary>
    /// Gets when the picture was taken.
    /// </summary>
    public DateTimeOffset Time { get; } = time;

    /// <summary>
    /// Gets what the detector found in it.
    /// </summary>
    public MotionDetectorResult Result { get; } = result;
}