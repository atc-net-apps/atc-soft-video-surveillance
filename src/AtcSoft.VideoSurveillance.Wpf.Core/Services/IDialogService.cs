namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// Service for displaying dialogs.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows the camera configuration dialog.
    /// </summary>
    /// <param name="camera">The camera to edit, or null for a new camera.</param>
    /// <param name="isNew">Whether this is a new camera.</param>
    /// <param name="existingEndpoints">Existing camera endpoints (IP address + path) to validate uniqueness against.</param>
    /// <param name="livePicture">
    /// Takes the picture the camera shows now, turned by its stored rotation, for drawing its motion
    /// zones on; <see langword="null"/> or a <see langword="null"/> picture opens the camera instead.
    /// </param>
    /// <returns>The configured camera if saved, or null if cancelled.</returns>
    CameraConfiguration? ShowCameraConfigurationDialog(
        CameraConfiguration? camera,
        bool isNew,
        IReadOnlyCollection<(string IpAddress, string? Path)> existingEndpoints,
        Func<CancellationToken, Task<byte[]?>>? livePicture = null);

    /// <summary>
    /// Shows an input box dialog.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="defaultText">The default input text.</param>
    /// <returns>The entered text if OK was clicked, or null if cancelled.</returns>
    string? ShowInputBox(
        string title,
        string prompt,
        string defaultText = "");

    /// <summary>
    /// Shows an input box dialog with validation against forbidden values.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="defaultText">The default input text.</param>
    /// <param name="forbiddenValues">Values that are not allowed (case-insensitive comparison).</param>
    /// <param name="forbiddenValueError">Error message to show when a forbidden value is entered.</param>
    /// <returns>The entered text if OK was clicked, or null if cancelled.</returns>
    string? ShowInputBox(
        string title,
        string prompt,
        string defaultText,
        IReadOnlyCollection<string> forbiddenValues,
        string forbiddenValueError);

    /// <summary>
    /// Shows a confirmation dialog.
    /// </summary>
    /// <param name="message">The confirmation message.</param>
    /// <param name="title">The dialog title.</param>
    /// <returns>True if confirmed, false otherwise.</returns>
    bool ShowConfirmation(
        string message,
        string title);

    /// <summary>
    /// Shows an error message dialog.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="title">The dialog title. Defaults to localized "Error" if null.</param>
    void ShowError(
        string message,
        string? title = null);

    /// <summary>
    /// Shows an information message dialog.
    /// </summary>
    /// <param name="message">The information message.</param>
    /// <param name="title">The dialog title. Defaults to localized "Information" if null.</param>
    void ShowInfo(
        string message,
        string? title = null);

    /// <summary>
    /// Shows the about dialog with application information.
    /// </summary>
    void ShowAboutDialog();

    /// <summary>
    /// Shows the check for updates dialog.
    /// </summary>
    void ShowCheckForUpdatesDialog();

    /// <summary>
    /// Shows a camera in fullscreen mode.
    /// </summary>
    /// <param name="camera">The camera to display.</param>
    /// <param name="sourceTile">Optional source tile for player lending (enables instant display without reconnection).</param>
    void ShowFullScreenCamera(
        CameraConfiguration camera,
        UserControls.CameraTile? sourceTile = null);

    /// <summary>
    /// Shows the settings dialog.
    /// </summary>
    /// <returns>True if settings were saved, false if cancelled.</returns>
    bool ShowSettingsDialog();

    /// <summary>
    /// Shows the assign camera to layout dialog with dual-list multi-select.
    /// </summary>
    /// <param name="layoutName">The name of the layout being edited.</param>
    /// <param name="availableCameras">The cameras available for assignment (not in current layout).</param>
    /// <param name="assignedCameras">The cameras currently assigned to the layout.</param>
    /// <returns>The final list of assigned cameras if confirmed, or null if cancelled.</returns>
    IReadOnlyCollection<CameraConfiguration>? ShowAssignCameraDialog(
        string layoutName,
        IReadOnlyCollection<CameraConfiguration> availableCameras,
        IReadOnlyCollection<CameraConfiguration> assignedCameras);

    /// <summary>
    /// Shows the recordings browser dialog.
    /// </summary>
    void ShowRecordingsBrowserDialog();
}