namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// Represents a camera's position within a layout.
/// </summary>
public class CameraLayoutItem
{
    /// <summary>
    /// Gets or sets the unique identifier for the layout item.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the identifier of the camera.
    /// </summary>
    public Guid CameraId { get; set; }

    /// <summary>
    /// Gets or sets the order number (position) of the camera in the layout.
    /// </summary>
    public int OrderNumber { get; set; }

    /// <inheritdoc />
    public override string ToString()
        => $"CameraLayoutItem {{ CameraId={CameraId.ToString().Substring(0, 8)}, Position={OrderNumber.ToString(CultureInfo.InvariantCulture)} }}";
}