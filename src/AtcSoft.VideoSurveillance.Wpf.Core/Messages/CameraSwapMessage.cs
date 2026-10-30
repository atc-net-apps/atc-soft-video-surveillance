namespace AtcSoft.VideoSurveillance.Wpf.Core.Messages;

/// <summary>
/// Message sent when a camera should be swapped with an adjacent camera.
/// </summary>
public class CameraSwapMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CameraSwapMessage"/> class.
    /// </summary>
    /// <param name="cameraId">The identifier of the camera to swap.</param>
    /// <param name="direction">The direction to swap.</param>
    public CameraSwapMessage(
        Guid cameraId,
        SwapDirection direction)
    {
        CameraId = cameraId;
        Direction = direction;
    }

    /// <summary>
    /// Gets the identifier of the camera to swap.
    /// </summary>
    public Guid CameraId { get; }

    /// <summary>
    /// Gets the direction to swap the camera.
    /// </summary>
    public SwapDirection Direction { get; }
}