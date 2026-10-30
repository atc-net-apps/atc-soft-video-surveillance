namespace AtcSoft.VideoSurveillance.Wpf.Core.Models;

/// <summary>
/// Root container for camera and layout persistence.
/// </summary>
public class CameraStorageData
{
    /// <summary>
    /// Gets the list of camera configurations.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for JSON deserialization")]
    [SuppressMessage("Design", "MA0016:Prefer using collection abstraction instead of implementation", Justification = "Required for JSON deserialization")]
    public List<CameraConfiguration> Cameras { get; init; } = [];

    /// <summary>
    /// Gets the list of layouts.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for JSON deserialization")]
    [SuppressMessage("Design", "MA0016:Prefer using collection abstraction instead of implementation", Justification = "Required for JSON deserialization")]
    public List<CameraLayout> Layouts { get; init; } = [];

    /// <summary>
    /// Gets or sets the identifier of the startup layout.
    /// </summary>
    public Guid? StartupLayoutId { get; set; }
}