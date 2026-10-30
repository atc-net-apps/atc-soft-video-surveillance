namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// Manager for camera wall operations including layouts, cameras, and state management.
/// </summary>
public interface ICameraWallManager : INotifyPropertyChanged
{
    /// <summary>
    /// Gets the collection of available layouts.
    /// </summary>
    ObservableCollection<CameraLayout> Layouts { get; }

    /// <summary>
    /// Gets or sets the currently active layout.
    /// </summary>
    CameraLayout? CurrentLayout { get; set; }

    /// <summary>
    /// Gets the startup layout.
    /// </summary>
    CameraLayout? SelectedStartupLayout { get; }

    /// <summary>
    /// Gets the number of cameras in the current layout.
    /// </summary>
    int CameraCount { get; }

    /// <summary>
    /// Gets the number of connected cameras.
    /// </summary>
    int ConnectedCount { get; }

    /// <summary>
    /// Gets the current status text.
    /// </summary>
    string StatusText { get; }

    /// <summary>
    /// Gets the camera grid control reference.
    /// </summary>
    CameraGrid? CameraGrid { get; }

    /// <summary>
    /// Occurs when the status text changes.
    /// </summary>
    event EventHandler<string>? StatusChanged;

    /// <summary>
    /// Initializes the manager with the camera grid control.
    /// </summary>
    /// <param name="cameraGridControl">The camera grid control.</param>
    void Initialize(CameraGrid cameraGridControl);

    /// <summary>
    /// Adds a new camera.
    /// </summary>
    void AddCamera();

    /// <summary>
    /// Adds a new camera with the source pre-set to USB / webcam. The
    /// dialog opens directly on the USB device picker so the operator
    /// can pick a local device without first toggling the source radio.
    /// </summary>
    void AddUsbCamera();

    /// <summary>
    /// Edits an existing camera.
    /// </summary>
    /// <param name="camera">The camera to edit.</param>
    void EditCamera(CameraConfiguration camera);

    /// <summary>
    /// Deletes a camera after confirmation.
    /// </summary>
    /// <param name="camera">The camera to delete.</param>
    void DeleteCamera(CameraConfiguration camera);

    /// <summary>
    /// Persists an in-place mutation of a camera configuration (e.g., rotation
    /// changed via tile context menu) without re-opening the edit dialog.
    /// </summary>
    /// <param name="camera">The mutated camera configuration.</param>
    void SaveCameraConfiguration(CameraConfiguration camera);

    /// <summary>
    /// Shows a camera in full screen.
    /// </summary>
    /// <param name="camera">The camera to show.</param>
    /// <param name="sourceTile">Optional source tile for player lending (enables instant display without reconnection).</param>
    void ShowFullScreen(
        CameraConfiguration camera,
        UserControls.CameraTile? sourceTile = null);

    /// <summary>
    /// Reconnects all cameras.
    /// </summary>
    void ReconnectAll();

    /// <summary>
    /// Creates a new layout.
    /// </summary>
    void CreateNewLayout();

    /// <summary>
    /// Renames the current layout.
    /// </summary>
    void RenameCurrentLayout();

    /// <summary>
    /// Assigns an existing camera to the current layout.
    /// </summary>
    void AssignCameraToLayout();

    /// <summary>
    /// Deletes the current layout.
    /// </summary>
    void DeleteCurrentLayout();

    /// <summary>
    /// Sets the current layout as the startup layout.
    /// </summary>
    void SetCurrentAsStartup();

    /// <summary>
    /// Saves the current layout.
    /// </summary>
    void SaveCurrentLayout();

    /// <summary>
    /// Handles connection state changes.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    void OnConnectionStateChanged(CameraConnectionChangedEventArgs e);

    /// <summary>
    /// Handles position changes.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    void OnPositionChanged(CameraPositionChangedEventArgs e);

    /// <summary>
    /// Gets a value indicating whether a new layout can be created.
    /// </summary>
    bool CanCreateNewLayout { get; }

    /// <summary>
    /// Gets a value indicating whether the current layout can be renamed.
    /// </summary>
    bool CanRenameCurrentLayout { get; }

    /// <summary>
    /// Gets a value indicating whether a camera can be assigned to the current layout.
    /// </summary>
    bool CanAssignCameraToLayout { get; }

    /// <summary>
    /// Gets a value indicating whether the current layout can be deleted.
    /// </summary>
    bool CanDeleteCurrentLayout { get; }

    /// <summary>
    /// Gets a value indicating whether the current layout can be set as startup.
    /// </summary>
    bool CanSetCurrentAsStartup { get; }

    /// <summary>
    /// Gets a value indicating whether all cameras can be reconnected.
    /// </summary>
    bool CanReconnectAll { get; }

    /// <summary>
    /// Shows the about dialog.
    /// </summary>
    void ShowAboutDialog();

    /// <summary>
    /// Shows the check for updates dialog.
    /// </summary>
    void ShowCheckForUpdatesDialog();

    /// <summary>
    /// Shows the settings dialog.
    /// </summary>
    void ShowSettingsDialog();

    /// <summary>
    /// Shows the recordings browser dialog.
    /// </summary>
    void ShowRecordingsBrowserDialog();

    /// <summary>
    /// Shows the timeline of every camera, from the recording folders.
    /// </summary>
    void ShowTimeline();

    /// <summary>
    /// Applies the display settings to the camera wall.
    /// </summary>
    void ApplyDisplaySettings();

    /// <summary>
    /// Gets a value indicating whether an update is available.
    /// </summary>
    bool IsUpdateAvailable { get; }

    /// <summary>
    /// Gets the version string of the available update.
    /// </summary>
    string UpdateVersion { get; }

    /// <summary>
    /// Checks for updates on startup and sets <see cref="IsUpdateAvailable"/> and <see cref="UpdateVersion"/> if a newer version exists.
    /// Silently does nothing on failure.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    Task CheckForUpdatesOnStartupAsync();

    /// <summary>
    /// Opens the latest MSI download URL in the default browser.
    /// </summary>
    void DownloadLatestUpdate();
}