namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// Service for managing application settings persistence.
/// </summary>
public interface IApplicationSettingsService
{
    /// <summary>
    /// Gets the current general settings.
    /// </summary>
    GeneralSettings General { get; }

    /// <summary>
    /// Gets the current camera display settings.
    /// </summary>
    CameraDisplayAppSettings CameraDisplay { get; }

    /// <summary>
    /// Gets the current connection settings.
    /// </summary>
    ConnectionAppSettings Connection { get; }

    /// <summary>
    /// Gets the current performance settings.
    /// </summary>
    PerformanceSettings Performance { get; }

    /// <summary>
    /// Gets the current motion detection settings.
    /// </summary>
    MotionDetectionSettings MotionDetection { get; }

    /// <summary>
    /// Gets the current recording settings.
    /// </summary>
    RecordingSettings Recording { get; }

    /// <summary>
    /// Gets the current advanced settings.
    /// </summary>
    AdvancedSettings Advanced { get; }

    /// <summary>
    /// Updates and saves the general settings.
    /// </summary>
    /// <param name="settings">The new settings to save.</param>
    void SaveGeneral(GeneralSettings settings);

    /// <summary>
    /// Updates and saves the camera display settings.
    /// </summary>
    /// <param name="settings">The new settings to save.</param>
    void SaveCameraDisplay(CameraDisplayAppSettings settings);

    /// <summary>
    /// Updates and saves the connection settings.
    /// </summary>
    /// <param name="settings">The new settings to save.</param>
    void SaveConnection(ConnectionAppSettings settings);

    /// <summary>
    /// Updates and saves the performance settings.
    /// </summary>
    /// <param name="settings">The new settings to save.</param>
    void SavePerformance(PerformanceSettings settings);

    /// <summary>
    /// Updates and saves the motion detection settings.
    /// </summary>
    /// <param name="settings">The new settings to save.</param>
    void SaveMotionDetection(MotionDetectionSettings settings);

    /// <summary>
    /// Updates and saves the recording settings.
    /// </summary>
    /// <param name="settings">The new settings to save.</param>
    void SaveRecording(RecordingSettings settings);

    /// <summary>
    /// Updates and saves the advanced settings.
    /// </summary>
    /// <param name="settings">The new settings to save.</param>
    void SaveAdvanced(AdvancedSettings settings);

    /// <summary>
    /// Applies default settings to a new camera configuration.
    /// </summary>
    /// <param name="camera">The camera configuration to apply defaults to.</param>
    void ApplyDefaultsToCamera(CameraConfiguration camera);

    /// <summary>
    /// Gets the effective value for a camera setting, using the camera override if set, otherwise the app default.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="camera">The camera configuration.</param>
    /// <param name="appDefault">The application default value.</param>
    /// <param name="overrideSelector">Function to select the override value from camera overrides.</param>
    /// <returns>The effective value (override if set, otherwise app default).</returns>
    T GetEffectiveValue<T>(
        CameraConfiguration camera,
        T appDefault,
        Func<CameraOverrides?, T?> overrideSelector)
        where T : struct;

    /// <summary>
    /// Gets the effective string value for a camera setting, using the camera override if set, otherwise the app default.
    /// </summary>
    /// <param name="camera">The camera configuration.</param>
    /// <param name="appDefault">The application default value.</param>
    /// <param name="overrideSelector">Function to select the override value from camera overrides.</param>
    /// <returns>The effective value (override if set, otherwise app default).</returns>
    string? GetEffectiveStringValue(
        CameraConfiguration camera,
        string? appDefault,
        Func<CameraOverrides?, string?> overrideSelector);

    /// <summary>
    /// Reloads settings from storage.
    /// </summary>
    void Load();

    /// <summary>
    /// Reloads settings from storage asynchronously. Required for backends
    /// where loading is naturally async (e.g. an HTTP-based settings store);
    /// the local-file implementation just wraps <see cref="Load"/>.
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// Saves all settings to storage.
    /// </summary>
    void Save();
}