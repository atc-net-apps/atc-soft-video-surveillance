// ReSharper disable RedundantArgumentDefaultValue
namespace AtcSoft.CameraWall.Wpf.Services;

/// <summary>
/// Default implementation of <see cref="ICameraWallManager"/>.
/// </summary>
[Registration(Lifetime.Singleton)]
public sealed partial class CameraWallManager : ObservableObject, ICameraWallManager, IDisposable
{
    private readonly ILogger<CameraWallManager> logger;
    private readonly ICameraStorageService storageService;
    private readonly IDialogService dialogService;
    private readonly IApplicationSettingsService settingsService;
    private readonly IRecordingService recordingService;
    private readonly IMotionDetectionService motionDetectionService;
    private readonly ITimelapseService timelapseService;
    private readonly IGitHubReleaseService gitHubReleaseService;
    private readonly IToastNotificationService toastNotificationService;
    private readonly IVideoPlayerFactory videoPlayerFactory;
    private readonly IUsbCameraWatcher usbCameraWatcher;
    private readonly ICameraConnectionTypeDetector? connectionTypeDetector;
    private readonly IRecordingPreviews? recordingPreviews;
    private readonly ConcurrentDictionary<Guid, byte> connectionTypeProbedCameras = new();

    // The summaries of the activity indexes already read, kept while the app runs.
    private readonly AtcSoft.VideoSurveillance.Helpers.Activity.ActivitySummaryCache activitySummaries = new();

    private bool sessionSwitchSubscribed;
    private bool usbWatcherSubscribed;
    private bool disposed;

    [ObservableProperty(DependentPropertyNames = [nameof(CanCreateNewLayout)])]
    private CameraGrid? cameraGrid;

    [ObservableProperty]
    private string statusText = Translations.Ready;

    [ObservableProperty(DependentPropertyNames = [nameof(CanReconnectAll)])]
    private int cameraCount;

    [ObservableProperty]
    private int connectedCount;

    [ObservableProperty]
    private CameraLayout? selectedStartupLayout;

    [ObservableProperty]
    private bool isUpdateAvailable;

    [ObservableProperty]
    private string updateVersion = string.Empty;

    private Uri? updateDownloadUrl;

    [ObservableProperty(
        AfterChangedCallback = nameof(OnCurrentLayoutChangedCallback),
        DependentPropertyNames = [nameof(CanDeleteCurrentLayout), nameof(CanSetCurrentAsStartup)])]
    private CameraLayout? currentLayout;

    /// <summary>
    /// Initializes a new instance of the <see cref="CameraWallManager"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="storageService">The camera storage service.</param>
    /// <param name="dialogService">The dialog service.</param>
    /// <param name="settingsService">The application settings service.</param>
    /// <param name="recordingService">The recording service.</param>
    /// <param name="motionDetectionService">The motion detection service.</param>
    /// <param name="timelapseService">The timelapse service.</param>
    /// <param name="gitHubReleaseService">The GitHub release service.</param>
    /// <param name="toastNotificationService">The toast notification service.</param>
    /// <param name="videoPlayerFactory">The video player factory.</param>
    /// <param name="usbCameraWatcher">The USB hot-plug watcher (Null fallback on non-Windows hosts is a no-op).</param>
    public CameraWallManager(
        ILogger<CameraWallManager> logger,
        ICameraStorageService storageService,
        IDialogService dialogService,
        IApplicationSettingsService settingsService,
        IRecordingService recordingService,
        IMotionDetectionService motionDetectionService,
        ITimelapseService timelapseService,
        IGitHubReleaseService gitHubReleaseService,
        IToastNotificationService toastNotificationService,
        IVideoPlayerFactory videoPlayerFactory,
        IUsbCameraWatcher usbCameraWatcher,
        ICameraConnectionTypeDetector? connectionTypeDetector = null,
        IRecordingPreviews? recordingPreviews = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(storageService);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(recordingService);
        ArgumentNullException.ThrowIfNull(motionDetectionService);
        ArgumentNullException.ThrowIfNull(timelapseService);
        ArgumentNullException.ThrowIfNull(gitHubReleaseService);
        ArgumentNullException.ThrowIfNull(toastNotificationService);
        ArgumentNullException.ThrowIfNull(videoPlayerFactory);
        ArgumentNullException.ThrowIfNull(usbCameraWatcher);

        this.logger = logger;
        this.storageService = storageService;
        this.dialogService = dialogService;
        this.settingsService = settingsService;
        this.recordingService = recordingService;
        this.motionDetectionService = motionDetectionService;
        this.timelapseService = timelapseService;
        this.gitHubReleaseService = gitHubReleaseService;
        this.toastNotificationService = toastNotificationService;
        this.videoPlayerFactory = videoPlayerFactory;
        this.usbCameraWatcher = usbCameraWatcher;
        this.connectionTypeDetector = connectionTypeDetector;
        this.recordingPreviews = recordingPreviews;

        Layouts = new ObservableCollection<CameraLayout>(storageService.GetAllLayouts());

        EnsureDefaultLayoutExists();
        LoadStartupLayout();

        // Subscribe to the watcher events here (not in Initialize) so the
        // recording-stop + unplugged-state-push side effects are testable
        // without a CameraGrid. The watcher's Start() — which on Windows
        // creates the WMI subscription — still happens in Initialize so
        // we don't burn resources before the UI is alive.
        usbCameraWatcher.DeviceArrived += OnUsbDeviceArrived;
        usbCameraWatcher.DeviceRemoved += OnUsbDeviceRemoved;
        usbWatcherSubscribed = true;
    }

    /// <inheritdoc />
    public event EventHandler<string>? StatusChanged;

    /// <inheritdoc />
    public ObservableCollection<CameraLayout> Layouts { get; }

    /// <inheritdoc />
    public bool CanCreateNewLayout => CameraGrid is not null;

    /// <inheritdoc />
    public bool CanRenameCurrentLayout => CurrentLayout is not null;

    /// <inheritdoc />
    public bool CanAssignCameraToLayout
        => CameraGrid is not null && CurrentLayout is not null;

    /// <inheritdoc />
    public bool CanDeleteCurrentLayout
        => CurrentLayout is not null &&
           Layouts.Count > 1 &&
           CurrentLayout.Id != storageService.StartupLayoutId &&
           CurrentLayout.Items.Count == 0;

    /// <inheritdoc />
    public bool CanSetCurrentAsStartup
        => CurrentLayout is not null &&
           CurrentLayout.Id != storageService.StartupLayoutId;

    /// <inheritdoc />
    public bool CanReconnectAll
        => CameraCount > 0;

    /// <inheritdoc />
    public void Initialize(CameraGrid cameraGridControl)
    {
        CameraGrid = cameraGridControl;

        // Inject services into the camera grid
        CameraGrid.RecordingService = recordingService;
        CameraGrid.MotionDetectionService = motionDetectionService;
        CameraGrid.TimelapseService = timelapseService;
        CameraGrid.ToastNotificationService = toastNotificationService;
        CameraGrid.VideoPlayerFactory = videoPlayerFactory;
        CameraGrid.MediaPipelineFactory = player => new VideoEngineMediaPipeline(player);
        CameraGrid.TileLogger = logger;

        ApplyDisplaySettings();
        LoadStartupCameras();

        // RDP disconnect/reconnect tears down the GPU desktop session, killing
        // the D3D11 device the cameras render through. WPF fires Unloaded on the
        // tiles during the disconnect but does not reliably fire Loaded on the
        // way back, so we cannot recover via tile lifecycle alone. SessionSwitch
        // is the deterministic signal — listen for it and rebuild the players
        // (which rebuilds the GpuAccelerator and reopens the RTSP streams).
        SubscribeToSessionSwitch();

        // Hot-plug toast notifications for stored USB cameras. The watcher
        // is started here (after Initialize, so the dispatcher we marshal
        // toasts to is the camera-grid's UI thread).
        SubscribeToUsbWatcher();
    }

    private void SubscribeToUsbWatcher()
    {
        // Event hookup happens in the constructor. This method just
        // kicks the watcher's underlying source (WMI on Windows) to
        // begin firing events — kept separate so production code goes
        // through Initialize but tests can fire events directly via
        // NSubstitute's Raise.EventWith without needing Start().
        usbCameraWatcher.Start();
    }

    private void OnUsbDeviceArrived(
        object? sender,
        UsbCameraEventArgs e)
    {
        ShowUsbHotPlugToast(e.Device.DeviceId, isArrival: true);
        UpdateTileUnpluggedState(e.Device.DeviceId, unplugged: false);
    }

    private void OnUsbDeviceRemoved(
        object? sender,
        UsbCameraEventArgs e)
    {
        ShowUsbHotPlugToast(e.Device.DeviceId, isArrival: false);
        UpdateTileUnpluggedState(e.Device.DeviceId, unplugged: true);

        // Stop any in-flight recording for an unplugged camera so the
        // blinking record dot clears via the normal RecordingStateChanged
        // path. Without this, the operator sees a still-blinking dot next
        // to a "Device unplugged" indicator — confusing, and the file
        // grows with no incoming packets anyway.
        var camera = ResolveStoredUsbCamera(e.Device.DeviceId);
        if (camera is not null)
        {
            recordingService.StopRecording(camera.Id);
        }
    }

    private void UpdateTileUnpluggedState(
        string deviceId,
        bool unplugged)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return;
        }

        var camera = ResolveStoredUsbCamera(deviceId);
        if (camera is null)
        {
            return;
        }

        var grid = CameraGrid;
        if (grid is null)
        {
            return;
        }

        // Watcher fires on a WMI thread (Windows) — marshal to the UI
        // dispatcher before touching DependencyProperties.
        if (grid.Dispatcher.CheckAccess())
        {
            grid.SetUsbDeviceUnplugged(camera.Id, unplugged);
        }
        else
        {
            _ = grid.Dispatcher.BeginInvoke(() => grid.SetUsbDeviceUnplugged(camera.Id, unplugged));
        }
    }

    private void ShowUsbHotPlugToast(
        string deviceId,
        bool isArrival)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return;
        }

        // The watcher fires on its own thread (WMI on Windows). Resolve
        // the camera + format the message off the UI thread, then marshal
        // the toast call into the dispatcher so the notification can
        // touch UI-affinity APIs safely.
        var camera = ResolveStoredUsbCamera(deviceId);
        if (camera is null)
        {
            return;
        }

        var displayName = camera.Display.DisplayName;
        var template = isArrival
            ? Translations.UsbCameraReconnectedToast1
            : Translations.UsbCameraUnpluggedToast1;
        var message = string.Format(CultureInfo.CurrentCulture, template, displayName);

        var dispatcher = CameraGrid?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            toastNotificationService.ShowInformation(displayName, message, useDesktop: true, expirationTime: TimeSpan.FromSeconds(5));
        }
        else
        {
            _ = dispatcher.BeginInvoke(() => toastNotificationService.ShowInformation(
                displayName,
                message,
                useDesktop: true,
                expirationTime: TimeSpan.FromSeconds(5)));
        }
    }

    private CameraConfiguration? ResolveStoredUsbCamera(string deviceId)
    {
        foreach (var camera in storageService.GetAllCameras())
        {
            if (camera.Connection.Source != CameraSource.Usb)
            {
                continue;
            }

            var usbDeviceId = camera.Connection.Usb?.DeviceId;
            if (!string.IsNullOrEmpty(usbDeviceId) &&
                string.Equals(usbDeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                return camera;
            }
        }

        return null;
    }

    private void SubscribeToSessionSwitch()
    {
        if (sessionSwitchSubscribed)
        {
            return;
        }

        Microsoft.Win32.SystemEvents.SessionSwitch += OnSessionSwitch;
        sessionSwitchSubscribed = true;
    }

    private void OnSessionSwitch(
        object sender,
        Microsoft.Win32.SessionSwitchEventArgs e)
    {
        // We only care about transitions back into a usable desktop session.
        // The disconnect side (RemoteDisconnect, ConsoleDisconnect, SessionLock)
        // does not need recovery — the players keep running, they just can't
        // render. The reconnect/unlock side is when we need to rebuild the GPU.
        if (e.Reason is not (Microsoft.Win32.SessionSwitchReason.RemoteConnect
            or Microsoft.Win32.SessionSwitchReason.ConsoleConnect
            or Microsoft.Win32.SessionSwitchReason.SessionUnlock))
        {
            return;
        }

        var grid = CameraGrid;
        if (grid is null)
        {
            return;
        }

        // SystemEvents handlers run on a private SystemEvents thread — must
        // marshal to the UI dispatcher before touching CameraGrid/tiles.
        _ = grid.Dispatcher.BeginInvoke(() =>
        {
            LogSessionSwitchReconnect(e.Reason.ToString());
            grid.RecreateConnectedPlayers();
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (sessionSwitchSubscribed)
        {
            Microsoft.Win32.SystemEvents.SessionSwitch -= OnSessionSwitch;
            sessionSwitchSubscribed = false;
        }

        if (usbWatcherSubscribed)
        {
            usbCameraWatcher.DeviceArrived -= OnUsbDeviceArrived;
            usbCameraWatcher.DeviceRemoved -= OnUsbDeviceRemoved;
            usbCameraWatcher.Stop();
            usbWatcherSubscribed = false;
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public void ApplyDisplaySettings()
    {
        if (CameraGrid is null)
        {
            return;
        }

        var general = settingsService.General;
        var display = settingsService.CameraDisplay;
        var connection = settingsService.Connection;

        // General settings
        CameraGrid.AutoConnectOnLoad = general.ConnectCamerasOnStartup;

        // Display settings
        CameraGrid.ShowOverlayTitle = display.ShowOverlayTitle;
        CameraGrid.ShowOverlayDescription = display.ShowOverlayDescription;
        CameraGrid.ShowOverlayConnectionStatus = display.ShowOverlayConnectionStatus;
        CameraGrid.ShowOverlayTime = display.ShowOverlayTime;
        CameraGrid.ShowOverlayQuickActions = display.ShowOverlayQuickActions;
        CameraGrid.ShowOverlayConnectionTypeIcon = display.ShowOverlayConnectionTypeIcon;
        CameraGrid.ConnectionTypeIconPlacement = display.ConnectionTypeIconPlacement;
        CameraGrid.OverlayOpacity = display.OverlayOpacity;
        CameraGrid.AllowDragAndDropReorder = display.AllowDragAndDropReorder;
        CameraGrid.AutoSave = display.AutoSaveLayoutChanges;
        CameraGrid.SnapshotPath = display.SnapshotPath;

        // Connection settings
        CameraGrid.ConnectionTimeoutSeconds = connection.ConnectionTimeoutSeconds;
        CameraGrid.ReconnectDelaySeconds = connection.ReconnectDelaySeconds;
        CameraGrid.AutoReconnectOnFailure = connection.AutoReconnectOnFailure;

        // Notification settings
        CameraGrid.ShowNotificationOnDisconnect = connection.ShowNotificationOnDisconnect;
        CameraGrid.ShowNotificationOnReconnect = connection.ShowNotificationOnReconnect;
        CameraGrid.PlayNotificationSound = connection.PlayNotificationSound;

        // Performance settings
        var performance = settingsService.Performance;
        var videoQualityChanged = CameraGrid.VideoQuality != performance.VideoQuality;
        var hwAccelChanged = CameraGrid.HardwareAcceleration != performance.HardwareAcceleration;

        CameraGrid.VideoQuality = performance.VideoQuality;
        CameraGrid.HardwareAcceleration = performance.HardwareAcceleration;

        // Recording settings
        var recording = settingsService.Recording;
        CameraGrid.EnableRecordingOnMotion = recording.EnableRecordingOnMotion;
        CameraGrid.EnableRecordingOnConnect = recording.EnableRecordingOnConnect;
        CameraGrid.TranscodeVideoCodec = recording.TranscodeVideoCodec;

        // Motion detection settings
        var motionDetection = settingsService.MotionDetection;
        CameraGrid.MotionSensitivity = motionDetection.Sensitivity;
        CameraGrid.MotionMinimumChangePercent = motionDetection.MinimumChangePercent;
        CameraGrid.MotionSmallestObjectPercent = motionDetection.SmallestObjectPercent;
        CameraGrid.MotionStoppedObjectSeconds = motionDetection.StoppedObjectSeconds;
        CameraGrid.MotionIgnoreLightingChanges = motionDetection.IgnoreLightingChanges;
        CameraGrid.MotionAnalysisFrameRate = motionDetection.AnalysisFrameRate;
        CameraGrid.MotionAnalysisWidth = motionDetection.AnalysisWidth;
        CameraGrid.MotionPostDurationSeconds = motionDetection.PostMotionDurationSeconds;
        CameraGrid.MotionCooldownSeconds = motionDetection.CooldownSeconds;

        // Bounding box settings
        var boundingBox = motionDetection.BoundingBox;
        CameraGrid.ShowBoundingBoxInGrid = boundingBox.ShowInGrid;
        CameraGrid.BoundingBoxColor = boundingBox.Color;
        CameraGrid.BoundingBoxThickness = boundingBox.Thickness;
        CameraGrid.BoundingBoxSmoothing = boundingBox.Smoothing;
        CameraGrid.MotionBoundingBoxMinArea = boundingBox.MinArea;
        CameraGrid.MotionBoundingBoxPadding = boundingBox.Padding;

        // Recreate players if performance settings changed
        if (videoQualityChanged || hwAccelChanged)
        {
            CameraGrid.RecreateConnectedPlayers();
        }
    }

    /// <inheritdoc />
    public void AddCamera()
        => AddCameraInternal(preSelectedSource: CameraSource.Network);

    /// <inheritdoc />
    public void AddUsbCamera()
        => AddCameraInternal(preSelectedSource: CameraSource.Usb);

    private void AddCameraInternal(CameraSource preSelectedSource)
    {
        UpdateStatus(Translations.AddCameraDialog);

        // Pre-build the camera so the dialog opens on the right source.
        // Passing null would let the dialog default to Network, defeating
        // the whole point of the USB-specific ribbon shortcut.
        var seedCamera = new CameraConfiguration
        {
            Display = { DisplayName = Translations.NewCamera },
        };
        seedCamera.Connection.Source = preSelectedSource;
        if (preSelectedSource == CameraSource.Usb)
        {
            seedCamera.Connection.Usb = new UsbConnectionSettings();
        }

        var existingEndpoints = GetExistingCameraEndpoints(excludeCameraId: null);
        var camera = dialogService.ShowCameraConfigurationDialog(seedCamera, isNew: true, existingEndpoints);

        if (camera is not null)
        {
            storageService.AddOrUpdateCamera(camera);
            Messenger.Default.Send(new CameraAddMessage(camera));
            SaveCurrentLayout();
            CameraCount = storageService.GetAllCameras().Count;
            UpdateStatus(string.Format(CultureInfo.CurrentCulture, Translations.AddedCamera1, camera.Display.DisplayName));
        }
        else
        {
            UpdateStatus(Translations.AddCameraCancelled);
        }
    }

    /// <inheritdoc />
    public void SaveCameraConfiguration(CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        storageService.AddOrUpdateCamera(camera);
    }

    public void EditCamera(CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        UpdateStatus(string.Format(CultureInfo.CurrentCulture, Translations.EditingCamera1, camera.Display.DisplayName));

        // Exclude current camera's endpoint when editing (allow keeping same IP+Path)
        var existingEndpoints = GetExistingCameraEndpoints(excludeCameraId: camera.Id);
        var result = dialogService.ShowCameraConfigurationDialog(
            camera,
            isNew: false,
            existingEndpoints,
            livePicture: cancellationToken => CameraGrid?.CapturePictureAsync(camera.Id, cancellationToken) ?? Task.FromResult<byte[]?>(null));

        if (result is not null)
        {
            storageService.AddOrUpdateCamera(camera);

            // A running analysis (the tile's motion watch, the recording's index) takes the edited
            // zones at once; the dialog has turned them with a new rotation already.
            motionDetectionService.UseZones(camera.Id, camera.MotionZones);

            // The dialog mutates the camera in place, so the CameraTile's Camera
            // dependency property reference is unchanged and OnCameraChanged
            // doesn't fire on its own. Force the running tile to re-read
            // display-time settings (rotation, overlay position, name, …).
            CameraGrid?.RefreshCameraDisplay(camera.Id);

            UpdateStatus(string.Format(CultureInfo.CurrentCulture, Translations.UpdatedCamera1, camera.Display.DisplayName));
        }
        else
        {
            UpdateStatus(Translations.EditCameraCancelled);
        }
    }

    private List<(string IpAddress, string? Path)> GetExistingCameraEndpoints(
        Guid? excludeCameraId)
        => storageService
            .GetAllCameras()
            .Where(c => excludeCameraId is null || c.Id != excludeCameraId)
            .Where(c => !string.IsNullOrWhiteSpace(c.Connection.IpAddress))
            .Select(c => (c.Connection.IpAddress, c.Connection.Path))
            .ToList();

    /// <inheritdoc />
    public void DeleteCamera(CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var confirmed = dialogService.ShowConfirmation(
            string.Format(CultureInfo.CurrentCulture, Translations.ConfirmDeleteCamera1, camera.Display.DisplayName),
            Translations.DeleteCamera);

        if (confirmed)
        {
            storageService.DeleteCamera(camera.Id);
            Messenger.Default.Send(new CameraRemoveMessage(camera.Id));
            SaveCurrentLayout();
            CameraCount = storageService.GetAllCameras().Count;
            UpdateStatus(string.Format(CultureInfo.CurrentCulture, Translations.DeletedCamera1, camera.Display.DisplayName));
        }
    }

    /// <inheritdoc />
    public void ShowFullScreen(
        CameraConfiguration camera,
        CameraTile? sourceTile = null)
    {
        ArgumentNullException.ThrowIfNull(camera);

        UpdateStatus(string.Format(CultureInfo.CurrentCulture, Translations.FullScreenCamera1, camera.Display.DisplayName));
        dialogService.ShowFullScreenCamera(camera, sourceTile);
    }

    /// <inheritdoc />
    public void ReconnectAll()
    {
        UpdateStatus(Translations.ReconnectingAllCameras);
        CameraGrid?.ReconnectAll();
        UpdateStatus(Translations.AllCamerasReconnected);
    }

    /// <inheritdoc />
    public void CreateNewLayout()
    {
        if (CameraGrid is null)
        {
            return;
        }

        var existingNames = Layouts.Select(l => l.Name).ToList();
        var layoutName = dialogService.ShowInputBox(
            Translations.NewLayout,
            Translations.EnterLayoutName,
            $"Layout {DateTime.Now:yyyy-MM-dd HH:mm}",
            existingNames,
            Translations.LayoutNameAlreadyExists);

        if (string.IsNullOrWhiteSpace(layoutName))
        {
            UpdateStatus(Translations.NewLayoutCancelled);
            return;
        }

        var layout = new CameraLayout
        {
            Name = layoutName,
            Items = [],
        };

        storageService.AddOrUpdateLayout(layout);
        Layouts.Add(layout);
        CurrentLayout = layout;

        UpdateStatus(string.Format(CultureInfo.CurrentCulture, Translations.LayoutCreated1, layout.Name));
    }

    /// <inheritdoc />
    public void RenameCurrentLayout()
    {
        if (CurrentLayout is null)
        {
            return;
        }

        var oldName = CurrentLayout.Name;

        // Exclude current layout name from forbidden values (allow keeping the same name)
        var existingNames = Layouts
            .Where(l => l.Id != CurrentLayout.Id)
            .Select(l => l.Name)
            .ToList();

        var newName = dialogService.ShowInputBox(
            Translations.RenameLayout,
            Translations.EnterNewLayoutName,
            oldName,
            existingNames,
            Translations.LayoutNameAlreadyExists);

        if (string.IsNullOrWhiteSpace(newName) || newName == oldName)
        {
            UpdateStatus(Translations.RenameLayoutCancelled);
            return;
        }

        CurrentLayout.Name = newName;
        CurrentLayout.ModifiedAt = DateTime.UtcNow;
        storageService.AddOrUpdateLayout(CurrentLayout);

        UpdateStatus(string.Format(CultureInfo.CurrentCulture, Translations.LayoutRenamed2, oldName, newName));
    }

    /// <inheritdoc />
    public void AssignCameraToLayout()
    {
        if (CameraGrid is null || CurrentLayout is null)
        {
            return;
        }

        var allCameras = storageService.GetAllCameras();
        var cameraDict = allCameras.ToDictionary(c => c.Id);

        // Get assigned cameras in their current layout order
        var assignedCameras = CurrentLayout.Items
            .Where(i => cameraDict.ContainsKey(i.CameraId))
            .OrderBy(i => i.OrderNumber)
            .Select(i => cameraDict[i.CameraId])
            .ToList();

        var assignedCameraIds = assignedCameras
            .Select(c => c.Id)
            .ToHashSet();

        var availableCameras = allCameras
            .Where(c => !assignedCameraIds.Contains(c.Id))
            .ToList();

        UpdateStatus(Translations.AssignCameraToLayout);

        var result = dialogService.ShowAssignCameraDialog(
            CurrentLayout.Name,
            availableCameras,
            assignedCameras);

        if (result is null)
        {
            UpdateStatus(Translations.AssignCameraCancelled);
            return;
        }

        ApplyLayoutCameraChanges(result, assignedCameraIds);
    }

    private void ApplyLayoutCameraChanges(
        IReadOnlyCollection<CameraConfiguration> newAssignedCameras,
        HashSet<Guid> previousAssignedIds)
    {
        if (CameraGrid is null || CurrentLayout is null)
        {
            return;
        }

        // Build a dictionary of existing layout items by camera ID to preserve their IDs
        var existingItemsDict = CurrentLayout.Items.ToDictionary(i => i.CameraId);

        // Build new layout items preserving existing IDs where possible
        var newLayoutItems = new List<CameraLayoutItem>();
        var orderNumber = 0;

        foreach (var camera in newAssignedCameras)
        {
            CameraLayoutItem item;
            if (existingItemsDict.TryGetValue(camera.Id, out var existingItem))
            {
                // Preserve existing item ID, update order
                item = existingItem;
                item.OrderNumber = orderNumber;
            }
            else
            {
                // New camera - create new layout item
                item = new CameraLayoutItem
                {
                    CameraId = camera.Id,
                    OrderNumber = orderNumber,
                };
            }

            newLayoutItems.Add(item);
            orderNumber++;
        }

        // Calculate what changed for status message
        var newAssignedIds = newAssignedCameras
            .Select(c => c.Id)
            .ToHashSet();

        var (addedCount, removedCount) = CalculateLayoutChangeCounts(newAssignedIds, previousAssignedIds);

        // Update the layout items directly
        CurrentLayout.Items = newLayoutItems;
        CurrentLayout.ModifiedAt = DateTime.UtcNow;
        storageService.AddOrUpdateLayout(CurrentLayout);

        // Reload the CameraGrid with the new order
        var allCameras = storageService.GetAllCameras();
        CameraGrid.ApplyLayout(CurrentLayout, allCameras);

        CameraCount = CameraGrid.CameraTiles?.Count ?? 0;
        OnPropertyChanged(nameof(CanDeleteCurrentLayout));
        UpdateLayoutChangeStatus(addedCount, removedCount);
    }

    private static (int Added, int Removed) CalculateLayoutChangeCounts(
        HashSet<Guid> newAssignedIds,
        HashSet<Guid> previousAssignedIds)
    {
        var added = newAssignedIds
            .Except(previousAssignedIds)
            .Count();

        var removed = previousAssignedIds
            .Except(newAssignedIds)
            .Count();

        return (added, removed);
    }

    private void UpdateLayoutChangeStatus(
        int addedCount,
        int removedCount)
    {
        if (addedCount > 0 || removedCount > 0)
        {
            UpdateStatus(string.Format(
                CultureInfo.CurrentCulture,
                Translations.LayoutCamerasUpdated2,
                addedCount,
                removedCount));
        }
        else
        {
            UpdateStatus(Translations.LayoutOrderUpdated);
        }
    }

    /// <inheritdoc />
    public void DeleteCurrentLayout()
    {
        if (CurrentLayout is null)
        {
            return;
        }

        var confirmed = dialogService.ShowConfirmation(
            string.Format(CultureInfo.CurrentCulture, Translations.ConfirmDeleteLayout1, CurrentLayout.Name),
            Translations.DeleteLayout);

        if (confirmed)
        {
            var layoutToDelete = CurrentLayout;
            storageService.DeleteLayout(layoutToDelete.Id);
            Layouts.Remove(layoutToDelete);
            CurrentLayout = Layouts.FirstOrDefault();

            UpdateStatus(string.Format(CultureInfo.CurrentCulture, Translations.DeletedLayout1, layoutToDelete.Name));
        }
    }

    /// <inheritdoc />
    public void SetCurrentAsStartup()
    {
        if (CurrentLayout is null)
        {
            return;
        }

        storageService.StartupLayoutId = CurrentLayout.Id;
        SelectedStartupLayout = CurrentLayout;

        OnPropertyChanged(nameof(CanSetCurrentAsStartup));
        OnPropertyChanged(nameof(CanDeleteCurrentLayout));
        UpdateStatus(string.Format(CultureInfo.CurrentCulture, Translations.SetStartupLayout1, CurrentLayout.Name));
    }

    /// <inheritdoc />
    public void SaveCurrentLayout()
    {
        if (CameraGrid is null || CurrentLayout is null)
        {
            return;
        }

        CurrentLayout.Items = CameraGrid.GetCurrentLayout();
        CurrentLayout.ModifiedAt = DateTime.UtcNow;
        storageService.AddOrUpdateLayout(CurrentLayout);
    }

    /// <inheritdoc />
    public void OnConnectionStateChanged(CameraConnectionChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        LogCameraConnectionStateChanged(
            e.Camera.Display.DisplayName,
            e.PreviousState.ToString(),
            e.NewState.ToString());

        if (e.NewState == ConnectionState.Connected)
        {
            var recordOnConnect = e.Camera.Overrides?.Recording.EnableRecordingOnConnect
                                  ?? settingsService.Recording.EnableRecordingOnConnect;
            LogCameraConnectedRecordOnConnect(
                e.Camera.Display.DisplayName,
                recordOnConnect);

            DetectConnectionTypeInBackground(e.Camera);
        }

        UpdateStatus(string.Format(CultureInfo.CurrentCulture, Translations.CameraStateChanged2, e.Camera.Display.DisplayName, e.NewState));
        UpdateConnectionCount(e);
    }

    /// <summary>
    /// Implicit connection-type detection after the first successful connect
    /// of a network camera whose type is still "not set". Probes once per camera
    /// per session, never overwrites a value, and persists a detected value.
    /// </summary>
    private void DetectConnectionTypeInBackground(CameraConfiguration camera)
    {
        if (connectionTypeDetector is null ||
            camera.Connection.Source != CameraSource.Network ||
            camera.Connection.ConnectionType != CameraConnectionType.None ||
            string.IsNullOrWhiteSpace(camera.Connection.IpAddress) ||
            !connectionTypeProbedCameras.TryAdd(camera.Id, 0))
        {
            return;
        }

        var ipAddress = camera.Connection.IpAddress;
        var userName = camera.Authentication.UserName;
        var password = camera.Authentication.Password;
        var dispatcher = CameraGrid?.Dispatcher ?? Application.Current?.Dispatcher;

        _ = Task.Run(async () =>
        {
            var detected = await connectionTypeDetector
                .DetectAsync(ipAddress, userName, password)
                .ConfigureAwait(false);

            if (detected == CameraConnectionType.None || dispatcher is null)
            {
                return;
            }

            await dispatcher.InvokeAsync(() => ApplyDetectedConnectionType(camera, detected));
        });
    }

    private void ApplyDetectedConnectionType(
        CameraConfiguration camera,
        CameraConnectionType detected)
    {
        var updated = CameraConnectionTypeHelper.ApplyDetected(camera.Connection.ConnectionType, detected, isExplicit: false);
        if (updated == camera.Connection.ConnectionType)
        {
            return;
        }

        camera.Connection.ConnectionType = updated;
        storageService.AddOrUpdateCamera(camera);
        LogConnectionTypeDetected(camera.Display.DisplayName, updated.ToString());
    }

    /// <inheritdoc />
    public void OnPositionChanged(CameraPositionChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        // Auto-save layout if enabled
        if (settingsService.CameraDisplay.AutoSaveLayoutChanges)
        {
            SaveCurrentLayout();
        }

        UpdateStatus(string.Format(CultureInfo.CurrentCulture, Translations.PositionChanged1, e.Camera.Display.DisplayName));
    }

    /// <inheritdoc />
    public void ShowAboutDialog()
        => dialogService.ShowAboutDialog();

    /// <inheritdoc />
    public void ShowCheckForUpdatesDialog()
        => dialogService.ShowCheckForUpdatesDialog();

    /// <inheritdoc />
    public void ShowSettingsDialog()
    {
        if (dialogService.ShowSettingsDialog())
        {
            ApplyDisplaySettings();
        }
    }

    /// <inheritdoc />
    public void ShowRecordingsBrowserDialog()
        => dialogService.ShowRecordingsBrowserDialog();

    /// <inheritdoc />
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Window disposes its view model on close via the Closed event handler.")]
    public void ShowTimeline()
    {
        var viewModel = new AtcSoft.CameraWall.Wpf.Timeline.LocalTimelineViewModel(
            () => [.. storageService.GetAllCameras().Select(c => new AtcSoft.VideoSurveillance.Models.TimelineCamera(c.Id, c.Display.DisplayName))],
            () => [GlobalRecordingPath(), .. storageService.GetAllCameras().Select(c => c.Overrides?.Recording.RecordingPath)],
            recordingService,
            activitySummaries,
            previews: recordingPreviews,
            verdictSaverFor: (cameraId, videoPath) => RecordingVerdictSavers.ToFile(
                videoPath,
                storageService.GetAllCameras().FirstOrDefault(c => c.Id == cameraId),
                settingsService.MotionDetection));
        new AtcSoft.CameraWall.Wpf.Timeline.LocalTimelineWindow(viewModel)
        {
            Owner = Application.Current.MainWindow,
        }.Show();
    }

    private string GlobalRecordingPath()
        => !string.IsNullOrEmpty(settingsService.Recording.RecordingPath)
            ? settingsService.Recording.RecordingPath
            : ApplicationPaths.DefaultRecordingsPath;

    /// <inheritdoc />
    public async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            var latest = await gitHubReleaseService
                .GetLatestVersionAsync()
                .ConfigureAwait(true);

            if (latest is null)
            {
                return;
            }

            if (!Version.TryParse(ApplicationHelper.GetVersion(), out var currentVersion) || latest <= currentVersion)
            {
                return;
            }

            updateDownloadUrl = await gitHubReleaseService
                .GetLatestMsiDownloadUrlAsync()
                .ConfigureAwait(true);

            UpdateVersion = latest.ToString(3);
            IsUpdateAvailable = true;

            LogUpdateAvailable(
                currentVersion.ToString(3),
                UpdateVersion);
        }
        catch
        {
            // Silently fail - startup update check is not critical
        }
    }

    /// <inheritdoc />
    public void DownloadLatestUpdate()
    {
        if (updateDownloadUrl is null)
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = updateDownloadUrl.ToString(),
                UseShellExecute = true,
            });
        }
        catch
        {
            // Silently fail if unable to open browser
        }
    }

    private void OnCurrentLayoutChangedCallback()
    {
        if (CameraGrid is null || CurrentLayout is null)
        {
            return;
        }

        var allCameras = storageService.GetAllCameras();
        var cameraDict = allCameras.ToDictionary(c => c.Id);

        CameraGrid.ApplyLayout(CurrentLayout, allCameras);
        CameraCount = CameraGrid.CameraTiles?.Count ?? 0;

        LogLayoutChanged(
            CurrentLayout.Name,
            CurrentLayout.Items.Count);

        // Log cameras in the new layout
        var recording = settingsService.Recording;
        var camerasInLayout = CurrentLayout.Items
            .Where(i => cameraDict.ContainsKey(i.CameraId))
            .OrderBy(i => i.OrderNumber)
            .Select(i => cameraDict[i.CameraId])
            .ToList();

        foreach (var camera in camerasInLayout)
        {
            var recordOnConnect = camera.Overrides?.Recording.EnableRecordingOnConnect ?? recording.EnableRecordingOnConnect;
            var recordOnMotion = camera.Overrides?.Recording.EnableRecordingOnMotion ?? recording.EnableRecordingOnMotion;

            LogCameraRecordingSettings(
                camera.Display.DisplayName,
                recordOnConnect,
                recordOnMotion);
        }

        UpdateStatus(string.Format(CultureInfo.CurrentCulture, Translations.LoadedLayout1, CurrentLayout.Name));
    }

    private void EnsureDefaultLayoutExists()
    {
        if (Layouts.Count > 0)
        {
            return;
        }

        var defaultLayout = new CameraLayout
        {
            Name = "Default",
        };

        storageService.AddOrUpdateLayout(defaultLayout);
        storageService.StartupLayoutId = defaultLayout.Id;
        Layouts.Add(defaultLayout);
    }

    private void LoadStartupLayout()
    {
        if (storageService.StartupLayoutId.HasValue)
        {
            SelectedStartupLayout = Layouts.FirstOrDefault(l => l.Id == storageService.StartupLayoutId.Value);
            CurrentLayout = SelectedStartupLayout;

            if (SelectedStartupLayout is not null)
            {
                LogLoadingStartupLayout(
                    SelectedStartupLayout.Name,
                    SelectedStartupLayout.Items.Count);
            }
        }
        else if (Layouts.Count > 0)
        {
            // If no startup layout is set, use the first layout
            CurrentLayout = Layouts[0];
            LogNoStartupLayoutUsingFirst(CurrentLayout.Name);
        }
    }

    private void LoadStartupCameras()
    {
        if (CameraGrid is null)
        {
            return;
        }

        var allCameras = storageService.GetAllCameras();
        var cameraDict = allCameras.ToDictionary(c => c.Id);

        // Determine which cameras to load based on layout
        List<CameraConfiguration> camerasToLoad;
        if (SelectedStartupLayout is not null)
        {
            CameraGrid.ApplyLayout(SelectedStartupLayout, allCameras);

            // Get cameras in layout order
            camerasToLoad = SelectedStartupLayout.Items
                .Where(i => cameraDict.ContainsKey(i.CameraId))
                .OrderBy(i => i.OrderNumber)
                .Select(i => cameraDict[i.CameraId])
                .ToList();
        }
        else
        {
            // Load all cameras if no startup layout
            foreach (var camera in allCameras)
            {
                Messenger.Default.Send(new CameraAddMessage(camera));
            }

            camerasToLoad = allCameras.ToList();
        }

        CameraCount = camerasToLoad.Count;

        // Log recording settings
        var recording = settingsService.Recording;
        LogRecordingSettings(
            recording.EnableRecordingOnConnect,
            recording.EnableRecordingOnMotion);

        // Log camera information for cameras in the current layout
        foreach (var camera in camerasToLoad)
        {
            var recordOnConnect = camera.Overrides?.Recording.EnableRecordingOnConnect ?? recording.EnableRecordingOnConnect;
            var recordOnMotion = camera.Overrides?.Recording.EnableRecordingOnMotion ?? recording.EnableRecordingOnMotion;

            LogCameraRecordingSettings(
                camera.Display.DisplayName,
                recordOnConnect,
                recordOnMotion);
        }

        UpdateStatus(string.Format(CultureInfo.CurrentCulture, Translations.LoadedCameras1, camerasToLoad.Count));
    }

    private void UpdateConnectionCount(CameraConnectionChangedEventArgs e)
    {
        var wasConnected = e.PreviousState == ConnectionState.Connected;
        var isConnected = e.NewState == ConnectionState.Connected;

        if (!wasConnected && isConnected)
        {
            ConnectedCount++;
        }
        else if (wasConnected && !isConnected)
        {
            ConnectedCount = Math.Max(0, ConnectedCount - 1);
        }
    }

    private void UpdateStatus(string status)
    {
        StatusText = status;
        StatusChanged?.Invoke(this, status);
    }
}