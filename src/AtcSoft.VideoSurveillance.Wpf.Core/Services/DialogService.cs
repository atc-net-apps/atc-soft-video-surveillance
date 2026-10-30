// ReSharper disable RedundantArgumentDefaultValue
namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// Default implementation of <see cref="IDialogService"/>.
/// </summary>
[Registration(Lifetime.Singleton)]
public class DialogService : IDialogService
{
    private readonly IApplicationSettingsService settingsService;
    private readonly IMotionDetectionService motionDetectionService;
    private readonly IVideoPlayerFactory videoPlayerFactory;
    private readonly AtcSoft.VideoSurveillance.Services.IUsbCameraEnumerator usbCameraEnumerator;
    private readonly AtcSoft.VideoSurveillance.Services.ICameraConnectionTypeDetector? connectionTypeDetector;
    private readonly ICameraStorageService? cameraStorage;
    private readonly IRecordingLayoutSynchronizer? recordingLayoutSynchronizer;
    private readonly IRecordingPreviews? recordingPreviews;
    private readonly ILoggerFactory loggerFactory;
    private RecordingsBrowserState? lastRecordingsBrowserState;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialogService"/> class.
    /// </summary>
    /// <param name="settingsService">The application settings service.</param>
    /// <param name="motionDetectionService">The motion detection service.</param>
    /// <param name="videoPlayerFactory">The video player factory.</param>
    /// <param name="usbCameraEnumerator">
    /// The USB camera enumerator. Both hosted apps register a real
    /// implementation via DI (Media Foundation on the standalone,
    /// gateway-backed on the API client); the Null fallback only
    /// surfaces on non-Windows hosts. Without this parameter the dialog
    /// VM defaulted to the Null enumerator and the device dropdown
    /// stayed empty no matter how many cameras were attached.
    /// </param>
    /// <param name="connectionTypeDetector">
    /// Optional connection-type detector (ONVIF). When not registered, the
    /// camera dialog hides its Detect button; the connection type can still
    /// be set by hand.
    /// </param>
    public DialogService(
        IApplicationSettingsService settingsService,
        IMotionDetectionService motionDetectionService,
        IVideoPlayerFactory videoPlayerFactory,
        AtcSoft.VideoSurveillance.Services.IUsbCameraEnumerator usbCameraEnumerator,
        AtcSoft.VideoSurveillance.Services.ICameraConnectionTypeDetector? connectionTypeDetector = null,
        IRecordingLayoutSynchronizer? recordingLayoutSynchronizer = null,
        ICameraStorageService? cameraStorage = null,
        IRecordingPreviews? recordingPreviews = null,
        ILoggerFactory? loggerFactory = null)
    {
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        this.motionDetectionService = motionDetectionService ?? throw new ArgumentNullException(nameof(motionDetectionService));
        this.videoPlayerFactory = videoPlayerFactory ?? throw new ArgumentNullException(nameof(videoPlayerFactory));
        this.usbCameraEnumerator = usbCameraEnumerator ?? throw new ArgumentNullException(nameof(usbCameraEnumerator));
        this.connectionTypeDetector = connectionTypeDetector;
        this.recordingLayoutSynchronizer = recordingLayoutSynchronizer;
        this.cameraStorage = cameraStorage;
        this.recordingPreviews = recordingPreviews;
        this.loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    /// <inheritdoc />
    public CameraConfiguration? ShowCameraConfigurationDialog(
        CameraConfiguration? camera,
        bool isNew,
        IReadOnlyCollection<(string IpAddress, string? Path)> existingEndpoints,
        Func<CancellationToken, Task<byte[]?>>? livePicture = null)
    {
        var cameraConfig = camera ?? new CameraConfiguration { Display = { DisplayName = Translations.NewCamera } };

        // Apply default settings to new cameras
        if (isNew)
        {
            settingsService.ApplyDefaultsToCamera(cameraConfig);
        }

        var viewModel = new CameraConfigurationDialogViewModel(
            cameraConfig,
            isNew,
            existingEndpoints,
            settingsService,
            videoPlayerFactory,
            usbCameraEnumerator,
            connectionTypeDetector,
            livePicture);

        var dialog = new CameraConfigurationDialog(viewModel)
        {
            Owner = Application.Current.MainWindow,
        };

        return dialog.ShowDialog() == true
            ? cameraConfig
            : null;
    }

    /// <inheritdoc />
    public string? ShowInputBox(
        string title,
        string prompt,
        string defaultText = "")
        => ShowInputBox(title, prompt, defaultText, [], string.Empty);

    /// <inheritdoc />
    public string? ShowInputBox(
        string title,
        string prompt,
        string defaultText,
        IReadOnlyCollection<string> forbiddenValues,
        string forbiddenValueError)
    {
        ArgumentNullException.ThrowIfNull(forbiddenValues);

        var labelTextBox = new LabelTextBox
        {
            LabelText = prompt,
            Text = defaultText,
            IsMandatory = true,
            MinLength = 1,
        };

        var dialogBox = new InputDialogBox(
            Application.Current.MainWindow!,
            title,
            labelTextBox);

        // Add validation for forbidden values
        if (forbiddenValues.Count > 0)
        {
            var forbiddenSet = new HashSet<string>(forbiddenValues, StringComparer.OrdinalIgnoreCase);

            labelTextBox.TextChanged += (_, _) =>
            {
                var currentText = labelTextBox.Text.Trim();
                var isForbidden = forbiddenSet.Contains(currentText);
                labelTextBox.ValidationText = isForbidden
                    ? forbiddenValueError
                    : string.Empty;
            };

            // Initial validation check
            var initialText = labelTextBox.Text.Trim();
            if (forbiddenSet.Contains(initialText))
            {
                labelTextBox.ValidationText = forbiddenValueError;
            }
        }

        var dialogResult = dialogBox.ShowDialog();
        if (dialogResult.HasValue && dialogResult.Value)
        {
            return ((LabelTextBox)dialogBox.Data).Text;
        }

        return null;
    }

    /// <inheritdoc />
    public bool ShowConfirmation(
        string message,
        string title)
    {
        var dialogBox = new QuestionDialogBox(
            Application.Current.MainWindow!,
            title,
            message)
        {
            Width = 400,
        };

        var dialogResult = dialogBox.ShowDialog();
        return dialogResult.HasValue && dialogResult.Value;
    }

    /// <inheritdoc />
    public void ShowError(
        string message,
        string? title = null)
    {
        var settings = new Atc.Wpf.Forms.Dialogs.DialogBoxSettings(
            Atc.Wpf.Forms.Dialogs.DialogBoxType.Ok,
            Atc.LogCategoryType.Error)
        {
            TitleBarText = title ?? Translations.Error,
            Width = 400,
        };
        var dialog = new InfoDialogBox(
            Application.Current.MainWindow!,
            settings,
            message);
        dialog.ShowDialog();
    }

    /// <inheritdoc />
    public void ShowInfo(
        string message,
        string? title = null)
    {
        var dialog = new InfoDialogBox(
            Application.Current.MainWindow!,
            title ?? Translations.Information,
            message)
        {
            Width = 400,
        };
        dialog.ShowDialog();
    }

    /// <inheritdoc />
    public void ShowAboutDialog()
    {
        var version = Atc.Helpers.AssemblyHelper.GetSystemVersion();
        var year = DateTime.Now.Year;

        var dialog = new AboutDialog(version.ToString(), year)
        {
            Owner = Application.Current.MainWindow,
        };

        dialog.ShowDialog();
    }

    /// <inheritdoc />
    public void ShowCheckForUpdatesDialog()
    {
        using var gitHubReleaseService = new GitHubReleaseService();
        var viewModel = new CheckForUpdatesDialogViewModel(gitHubReleaseService);
        var dialog = new CheckForUpdatesDialog(viewModel)
        {
            Owner = Application.Current.MainWindow,
        };

        dialog.ShowDialog();
    }

    /// <inheritdoc />
    public void ShowFullScreenCamera(
        CameraConfiguration camera,
        CameraTile? sourceTile = null)
    {
        ArgumentNullException.ThrowIfNull(camera);

        // Compute effective overlay settings (per-camera overrides → app defaults)
        var display = settingsService.CameraDisplay;
        var overrides = camera.Overrides;

        var showOverlayTitle = overrides?.CameraDisplay.ShowOverlayTitle ?? display.ShowOverlayTitle;
        var showOverlayDescription = overrides?.CameraDisplay.ShowOverlayDescription ?? display.ShowOverlayDescription;
        var showOverlayTime = overrides?.CameraDisplay.ShowOverlayTime ?? display.ShowOverlayTime;
        var showOverlayConnectionStatus = overrides?.CameraDisplay.ShowOverlayConnectionStatus ?? display.ShowOverlayConnectionStatus;
        var overlayOpacity = overrides?.CameraDisplay.OverlayOpacity ?? display.OverlayOpacity;
        var overlayPosition = camera.Display.OverlayPosition;

        // Compute effective bounding box settings (per-camera overrides → app defaults)
        var motionDetection = settingsService.MotionDetection;
        var showBoundingBoxInFullScreen = overrides?.MotionDetection.BoundingBox.ShowInFullScreen ?? motionDetection.BoundingBox.ShowInFullScreen;
        var boundingBoxColor = overrides?.MotionDetection.BoundingBox.Color ?? motionDetection.BoundingBox.Color;
        var boundingBoxThickness = overrides?.MotionDetection.BoundingBox.Thickness ?? motionDetection.BoundingBox.Thickness;
        var boundingBoxSmoothing = overrides?.MotionDetection.BoundingBox.Smoothing ?? motionDetection.BoundingBox.Smoothing;

        // Borrow player from source tile for instant display (no reconnection delay)
        var borrowedPlayer = sourceTile?.LendPlayer();

        try
        {
            using var viewModel = new FullScreenCameraWindowViewModel(
                camera,
                showOverlayTitle,
                showOverlayDescription,
                showOverlayTime,
                showOverlayConnectionStatus,
                overlayOpacity,
                overlayPosition,
                showBoundingBoxInFullScreen,
                boundingBoxColor,
                boundingBoxThickness,
                boundingBoxSmoothing,
                motionDetectionService,
                borrowedPlayer,
                videoPlayerFactory,
                overrides?.CameraDisplay.ShowOverlayConnectionTypeIcon ?? display.ShowOverlayConnectionTypeIcon,
                overrides?.CameraDisplay.ConnectionTypeIconPlacement ?? display.ConnectionTypeIconPlacement);
            using var window = new FullScreenCameraWindow(viewModel);
            window.Owner = Application.Current.MainWindow;

            window.ShowDialog();
        }
        finally
        {
            // Return the borrowed player to the source tile
            if (sourceTile is not null && borrowedPlayer is not null)
            {
                sourceTile.ReturnPlayer(borrowedPlayer);
            }
        }
    }

    /// <inheritdoc />
    public bool ShowSettingsDialog()
    {
        // Set CultureManager.UiCulture to saved language before dialog loads
        // This ensures LabelLanguageSelector shows the correct language on initialization
        // (LanguageSelector falls back to Thread.CurrentThread.CurrentUICulture.LCID when SelectedKey is empty)
        if (NumberHelper.TryParseToInt(settingsService.General.Language, out var lcid))
        {
            CultureManager.UiCulture = new CultureInfo(lcid);
        }

        var viewModel = new SettingsDialogViewModel(settingsService, recordingLayoutSynchronizer, loggerFactory.CreateLogger<SettingsDialogViewModel>());
        var dialog = new SettingsDialog(viewModel)
        {
            Owner = Application.Current.MainWindow,
        };

        return dialog.ShowDialog() == true;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<CameraConfiguration>? ShowAssignCameraDialog(
        string layoutName,
        IReadOnlyCollection<CameraConfiguration> availableCameras,
        IReadOnlyCollection<CameraConfiguration> assignedCameras)
    {
        ArgumentNullException.ThrowIfNull(layoutName);
        ArgumentNullException.ThrowIfNull(availableCameras);
        ArgumentNullException.ThrowIfNull(assignedCameras);

        var viewModel = new AssignCameraDialogViewModel(layoutName, availableCameras, assignedCameras);
        var dialog = new AssignCameraDialog(viewModel)
        {
            Owner = Application.Current.MainWindow,
        };

        // Only return result if dialog was confirmed AND there are actual changes
        if (dialog.ShowDialog() == true && viewModel.HasActualChanges())
        {
            return viewModel.GetAssignedCameras();
        }

        return null;
    }

    /// <inheritdoc />
    public void ShowRecordingsBrowserDialog()
    {
        var viewModel = new RecordingsBrowserDialogViewModel(
            settingsService,
            () => cameraStorage?.GetAllCameras().Select(c => c.Overrides?.Recording.RecordingPath) ?? [],
            folder => cameraStorage?.GetAllCameras()
                .FirstOrDefault(c => string.Equals(
                    AtcSoft.VideoSurveillance.Helpers.RecordingPathHelper.GetCameraFolderName(c.Display.DisplayName, c.Id),
                    folder,
                    StringComparison.OrdinalIgnoreCase))
                ?.Core)
        {
            Previews = recordingPreviews,
        };
        if (settingsService.Recording.RememberRecordingsBrowserState &&
            lastRecordingsBrowserState is not null)
        {
            viewModel.RestoreState(lastRecordingsBrowserState);
        }

        using var dialog = new RecordingsBrowserDialog(viewModel)
        {
            Owner = Application.Current.MainWindow,
        };
        dialog.ShowDialog();

        lastRecordingsBrowserState = viewModel.CaptureState();
    }
}