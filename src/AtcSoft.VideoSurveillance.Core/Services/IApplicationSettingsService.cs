namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Service for managing application settings persistence.
/// </summary>
public interface IApplicationSettingsService
{
    GeneralSettings General { get; }

    CameraDisplayAppSettings CameraDisplay { get; }

    ConnectionAppSettings Connection { get; }

    PerformanceSettings Performance { get; }

    MotionDetectionSettings MotionDetection { get; }

    RecordingSettings Recording { get; }

    AdvancedSettings Advanced { get; }

    void SaveGeneral(GeneralSettings settings);

    void SaveCameraDisplay(CameraDisplayAppSettings settings);

    void SaveConnection(ConnectionAppSettings settings);

    void SavePerformance(PerformanceSettings settings);

    void SaveMotionDetection(MotionDetectionSettings settings);

    void SaveRecording(RecordingSettings settings);

    void SaveAdvanced(AdvancedSettings settings);

    void ApplyDefaultsToCamera(CameraConfiguration camera);

    T GetEffectiveValue<T>(
        CameraConfiguration camera,
        T appDefault,
        Func<CameraOverrides?, T?> overrideSelector)
        where T : struct;

    string? GetEffectiveStringValue(
        CameraConfiguration camera,
        string? appDefault,
        Func<CameraOverrides?, string?> overrideSelector);

    void Load();

    void Save();
}