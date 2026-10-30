namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Service interface for persisting camera configurations and layouts.
/// </summary>
public interface ICameraStorageService
{
    IReadOnlyList<CameraConfiguration> GetAllCameras();

    CameraConfiguration? GetCameraById(Guid id);

    void AddOrUpdateCamera(CameraConfiguration camera);

    bool DeleteCamera(Guid id);

    IReadOnlyList<CameraLayout> GetAllLayouts();

    CameraLayout? GetLayoutById(Guid id);

    void AddOrUpdateLayout(CameraLayout layout);

    bool DeleteLayout(Guid id);

    Guid? StartupLayoutId { get; set; }

    void Save();

    void Load();
}