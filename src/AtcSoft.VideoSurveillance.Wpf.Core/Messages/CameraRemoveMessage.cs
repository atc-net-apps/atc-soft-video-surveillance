namespace AtcSoft.VideoSurveillance.Wpf.Core.Messages;

/// <summary>
/// Message sent when a camera should be removed from the camera wall.
/// </summary>
public class CameraRemoveMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CameraRemoveMessage"/> class.
    /// </summary>
    /// <param name="cameraId">The identifier of the camera to remove.</param>
    public CameraRemoveMessage(Guid cameraId)
        => CameraId = cameraId;

    /// <summary>
    /// Gets the identifier of the camera to remove.
    /// </summary>
    public Guid CameraId { get; }
}