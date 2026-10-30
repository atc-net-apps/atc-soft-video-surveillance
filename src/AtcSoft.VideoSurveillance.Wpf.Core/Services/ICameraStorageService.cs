// ReSharper disable ArrangeTypeMemberModifiers
namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// Service interface for persisting camera configurations and layouts.
/// </summary>
public interface ICameraStorageService
{
    /// <summary>
    /// Gets all camera configurations.
    /// </summary>
    /// <returns>A read-only list of camera configurations.</returns>
    IReadOnlyList<CameraConfiguration> GetAllCameras();

    /// <summary>
    /// Gets a camera configuration by its identifier.
    /// </summary>
    /// <param name="id">The camera identifier.</param>
    /// <returns>The camera configuration, or null if not found.</returns>
    CameraConfiguration? GetCameraById(Guid id);

    /// <summary>
    /// Adds or updates a camera configuration.
    /// </summary>
    /// <param name="camera">The camera configuration to add or update.</param>
    void AddOrUpdateCamera(CameraConfiguration camera);

    /// <summary>
    /// Deletes a camera configuration.
    /// </summary>
    /// <param name="id">The camera identifier.</param>
    /// <returns>True if the camera was deleted, false if not found.</returns>
    bool DeleteCamera(Guid id);

    /// <summary>
    /// Gets all layouts.
    /// </summary>
    /// <returns>A read-only list of layouts.</returns>
    IReadOnlyList<CameraLayout> GetAllLayouts();

    /// <summary>
    /// Gets a layout by its identifier.
    /// </summary>
    /// <param name="id">The layout identifier.</param>
    /// <returns>The layout, or null if not found.</returns>
    CameraLayout? GetLayoutById(Guid id);

    /// <summary>
    /// Adds or updates a layout.
    /// </summary>
    /// <param name="layout">The layout to add or update.</param>
    void AddOrUpdateLayout(CameraLayout layout);

    /// <summary>
    /// Deletes a layout.
    /// </summary>
    /// <param name="id">The layout identifier.</param>
    /// <returns>True if the layout was deleted, false if not found.</returns>
    bool DeleteLayout(Guid id);

    /// <summary>
    /// Gets or sets the identifier of the startup layout.
    /// </summary>
    Guid? StartupLayoutId { get; set; }

    /// <summary>
    /// Saves all changes to persistent storage.
    /// </summary>
    void Save();

    /// <summary>
    /// Loads data from persistent storage.
    /// </summary>
    void Load();
}