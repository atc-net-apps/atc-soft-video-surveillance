namespace AtcSoft.VideoSurveillance.Wpf.Core.Messages;

/// <summary>
/// Message sent when a camera should be added to the camera wall.
/// </summary>
public class CameraAddMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CameraAddMessage"/> class.
    /// </summary>
    /// <param name="camera">The camera configuration to add.</param>
    public CameraAddMessage(CameraConfiguration camera)
        => Camera = camera ?? throw new ArgumentNullException(nameof(camera));

    /// <summary>
    /// Gets the camera configuration to add.
    /// </summary>
    public CameraConfiguration Camera { get; }
}