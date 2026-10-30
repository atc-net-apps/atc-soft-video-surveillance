namespace AtcSoft.CameraWall.Wpf.App;

/// <summary>
/// View model for the main window. Acts as a thin binding layer between the UI and the camera wall manager.
/// </summary>
public partial class MainWindowViewModel : MainWindowViewModelBase
{
    // The disk is looked at this often; it fills slowly.
    private static readonly TimeSpan DiskSpaceRefreshInterval = TimeSpan.FromSeconds(30);

    private readonly IDialogService dialogService;
    private readonly IApplicationSettingsService settingsService;
    private readonly DispatcherTimer diskSpaceTimer;
    private CameraGrid? cameraGrid;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    /// <param name="cameraWallManager">The camera wall manager.</param>
    /// <param name="settingsService">The application settings service.</param>
    /// <param name="dialogService">The dialog service.</param>
    public MainWindowViewModel(
        ICameraWallManager cameraWallManager,
        IApplicationSettingsService settingsService,
        IDialogService dialogService)
    {
        ArgumentNullException.ThrowIfNull(cameraWallManager);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(dialogService);

        this.dialogService = dialogService;
        this.settingsService = settingsService;
        Manager = cameraWallManager;

        // Recordings stop when the disk runs out, so the status bar shows
        // how much is left before it does.
        diskSpaceTimer = new DispatcherTimer { Interval = DiskSpaceRefreshInterval };
        diskSpaceTimer.Tick += (_, _) => RefreshDiskSpace();
        diskSpaceTimer.Start();
        RefreshDiskSpace();
        Manager.PropertyChanged += OnManagerPropertyChanged;
        Manager.Layouts.CollectionChanged += OnLayoutsCollectionChanged;

        IsRibbonMinimized = settingsService.General.StartRibbonCollapsed;
        RebuildLayoutItems();
        SubscribeToLayoutPropertyChanges();
    }

    /// <summary>
    /// Gets the camera wall manager.
    /// </summary>
    public ICameraWallManager Manager { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the ribbon is minimized (collapsed).
    /// </summary>
    [ObservableProperty]
    private bool isRibbonMinimized;

    /// <summary>
    /// Gets the collection of available layouts.
    /// </summary>
    public ObservableCollection<CameraLayout> Layouts
        => Manager.Layouts;

    /// <summary>
    /// Gets or sets the currently active layout.
    /// </summary>
    public CameraLayout? CurrentLayout
    {
        get => Manager.CurrentLayout;
        set => Manager.CurrentLayout = value;
    }

    /// <summary>
    /// Gets the layout items as a dictionary for LabelComboBox binding.
    /// Key = layout ID (string), Value = layout name.
    /// </summary>
    [ObservableProperty]
    private Dictionary<string, string> layoutItems = [];

    /// <summary>
    /// Gets or sets the selected layout ID for LabelComboBox binding.
    /// Auto-syncs with <see cref="CurrentLayout"/>.
    /// </summary>
    public string? SelectedLayoutId
    {
        get => Manager.CurrentLayout?.Id.ToString();
        set
        {
            if (value is null ||
                !Guid.TryParse(value, out var id))
            {
                return;
            }

            var layout = Manager.Layouts.FirstOrDefault(l => l.Id == id);
            if (layout is not null)
            {
                Manager.CurrentLayout = layout;
            }
        }
    }

    /// <summary>
    /// Gets the startup layout.
    /// </summary>
    public CameraLayout? SelectedStartupLayout
        => Manager.SelectedStartupLayout;

    /// <summary>
    /// Gets the number of cameras in the current layout.
    /// </summary>
    public int CameraCount
        => Manager.CameraCount;

    /// <summary>
    /// Gets the number of connected cameras.
    /// </summary>
    public int ConnectedCount
        => Manager.ConnectedCount;

    /// <summary>
    /// Gets the current status text.
    /// </summary>
    public string StatusText
        => Manager.StatusText;

    /// <summary>
    /// Gets a value indicating whether an update is available.
    /// </summary>
    public bool IsUpdateAvailable
        => Manager.IsUpdateAvailable;

    /// <summary>
    /// Gets the version string of the available update.
    /// </summary>
    public string UpdateVersion
        => Manager.UpdateVersion;

    /// <summary>
    /// Initializes the view model with the camera grid control.
    /// </summary>
    /// <param name="cameraGridControl">The camera grid control.</param>
    public void Initialize(CameraGrid cameraGridControl)
    {
        ArgumentNullException.ThrowIfNull(cameraGridControl);

        Manager.Initialize(cameraGridControl);

        cameraGrid = cameraGridControl;
        cameraGrid.GridPreset = GridPager.ParsePreset(
            settingsService.General.GridSize.ToString(CultureInfo.InvariantCulture));
        cameraGrid.PagingChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SelectedGridPreset));
            OnPropertyChanged(nameof(PageLabel));
            OnPropertyChanged(nameof(HasPages));
            CommandManager.InvalidateRequerySuggested();
        };
    }

    /// <summary>
    /// Gets how full the disk with the recordings is ("120 GB free of 500 GB");
    /// empty when the disk cannot be read.
    /// </summary>
    public string DiskSpaceText { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether less is free than the recordings need.
    /// </summary>
    public bool IsDiskSpaceLow { get; private set; }

    /// <summary>
    /// Gets a value indicating whether there is a disk status to show.
    /// </summary>
    public bool HasDiskSpace => DiskSpaceText.Length > 0;

    private void RefreshDiskSpace()
    {
        var recording = settingsService.Recording;
        var status = AtcSoft.VideoSurveillance.Helpers.DiskSpaceStatus.Read(
            recording.RecordingPath,
            recording.Cleanup.MinFreeSpaceMb,
            recording.Cleanup.EnableDiskSpaceGuard);

        DiskSpaceText = status?.Text ?? string.Empty;
        IsDiskSpaceLow = status?.IsLow ?? false;
        OnPropertyChanged(nameof(DiskSpaceText));
        OnPropertyChanged(nameof(IsDiskSpaceLow));
        OnPropertyChanged(nameof(HasDiskSpace));
    }

    /// <summary>
    /// Gets the grid sizes to choose from.
    /// </summary>
    public IDictionary<string, string> GridPresetItems { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["0"] = Translations.GridSizeAuto,
        ["1"] = "1 \u00D7 1",
        ["2"] = "2 \u00D7 2",
        ["3"] = "3 \u00D7 3",
        ["4"] = "4 \u00D7 4",
    };

    /// <summary>
    /// Gets or sets the grid's size, as its number of cameras along a side ("0" is automatic).
    /// </summary>
    public string SelectedGridPreset
    {
        get => ((int)(cameraGrid?.GridPreset ?? GridPreset.Auto)).ToString(CultureInfo.InvariantCulture);
        set => SelectGridPreset(value);
    }

    /// <summary>
    /// Gets the page of cameras shown ("2 / 3"); empty when there is one page.
    /// </summary>
    public string PageLabel => cameraGrid?.PageLabel ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether the cameras take more than one page.
    /// </summary>
    public bool HasPages => cameraGrid is { PageCount: > 1 };

    /// <summary>
    /// Sets the grid's size (Ctrl+Alt+0 to Ctrl+Alt+4) and goes back to the first page.
    /// </summary>
    [RelayCommand]
    private void SelectGridPreset(string? side)
    {
        if (cameraGrid is null)
        {
            return;
        }

        cameraGrid.GridPreset = GridPager.ParsePreset(side);
        cameraGrid.CurrentPage = 0;

        // Remembered for the next start.
        var size = (int)cameraGrid.GridPreset;
        if (settingsService.General.GridSize != size)
        {
            var general = settingsService.General.Clone();
            general.GridSize = size;
            settingsService.SaveGeneral(general);
        }
    }

    [RelayCommand(CanExecute = nameof(HasPages))]
    private void NextPage()
        => cameraGrid?.ShowNextPage();

    [RelayCommand(CanExecute = nameof(HasPages))]
    private void PreviousPage()
        => cameraGrid?.ShowPreviousPage();

    /// <summary>
    /// Shows a camera in full screen.
    /// </summary>
    /// <param name="camera">The camera to show.</param>
    /// <param name="sourceTile">Optional source tile for player lending.</param>
    public void ShowFullScreen(
        CameraConfiguration camera,
        CameraTile? sourceTile = null)
        => Manager.ShowFullScreen(camera, sourceTile);

    /// <summary>
    /// Handles connection state changes.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    public void OnConnectionStateChanged(CameraConnectionChangedEventArgs e)
        => Manager.OnConnectionStateChanged(e);

    /// <summary>
    /// Handles position changes.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    public void OnPositionChanged(CameraPositionChangedEventArgs e)
        => Manager.OnPositionChanged(e);

    /// <summary>
    /// Opens the camera configuration dialog for editing.
    /// </summary>
    /// <param name="camera">The camera to edit.</param>
    public void EditCamera(CameraConfiguration camera)
        => Manager.EditCamera(camera);

    /// <summary>
    /// Deletes a camera after confirmation.
    /// </summary>
    /// <param name="camera">The camera to delete.</param>
    public void DeleteCamera(CameraConfiguration camera)
        => Manager.DeleteCamera(camera);

    /// <summary>
    /// Persists an in-place camera configuration mutation (e.g., rotation
    /// changed via tile context menu).
    /// </summary>
    public void SaveCameraConfiguration(CameraConfiguration camera)
        => Manager.SaveCameraConfiguration(camera);

    private void OnLayoutsCollectionChanged(
        object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // Unsubscribe from removed layouts
        if (e.OldItems is not null)
        {
            foreach (CameraLayout layout in e.OldItems)
            {
                layout.PropertyChanged -= OnLayoutPropertyChanged;
            }
        }

        // Subscribe to new layouts
        if (e.NewItems is not null)
        {
            foreach (CameraLayout layout in e.NewItems)
            {
                layout.PropertyChanged += OnLayoutPropertyChanged;
            }
        }

        RebuildLayoutItems();
    }

    private void OnLayoutPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CameraLayout.Name))
        {
            RebuildLayoutItems();
        }
    }

    private void SubscribeToLayoutPropertyChanges()
    {
        foreach (var layout in Manager.Layouts)
        {
            layout.PropertyChanged += OnLayoutPropertyChanged;
        }
    }

    private void RebuildLayoutItems()
    {
        LayoutItems = Manager.Layouts.ToDictionary(
            l => l.Id.ToString(),
            l => l.Name,
            StringComparer.Ordinal);

        // Re-notify SelectedLayoutId so the ComboBox re-applies the display text
        OnPropertyChanged(nameof(SelectedLayoutId));
    }

    private void OnManagerPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        // Forward property change notifications only for properties exposed on this ViewModel
        if (e.PropertyName
            is nameof(CurrentLayout)
            or nameof(SelectedStartupLayout)
            or nameof(CameraCount)
            or nameof(ConnectedCount)
            or nameof(StatusText)
            or nameof(IsUpdateAvailable)
            or nameof(UpdateVersion))
        {
            OnPropertyChanged(e.PropertyName);
        }

        // Sync SelectedLayoutId when CurrentLayout changes externally
        if (e.PropertyName is nameof(CurrentLayout))
        {
            OnPropertyChanged(nameof(SelectedLayoutId));
        }

        // Handle CanExecute invalidation for commands
        if (e.PropertyName
            is nameof(ICameraWallManager.CameraCount)
            or nameof(ICameraWallManager.CameraGrid)
            or nameof(ICameraWallManager.CurrentLayout)
            or nameof(ICameraWallManager.CanCreateNewLayout)
            or nameof(ICameraWallManager.CanRenameCurrentLayout)
            or nameof(ICameraWallManager.CanAssignCameraToLayout)
            or nameof(ICameraWallManager.CanDeleteCurrentLayout)
            or nameof(ICameraWallManager.CanSetCurrentAsStartup)
            or nameof(ICameraWallManager.CanReconnectAll))
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    [RelayCommand]
    private void AddCamera()
        => Manager.AddCamera();

    [RelayCommand]
    private void AddUsbCamera()
        => Manager.AddUsbCamera();

    [RelayCommand(CanExecute = nameof(CanNewLayout))]
    private void NewLayout()
        => Manager.CreateNewLayout();

    private bool CanNewLayout()
        => Manager.CanCreateNewLayout;

    [RelayCommand(CanExecute = nameof(CanRenameLayout))]
    private void RenameLayout()
        => Manager.RenameCurrentLayout();

    private bool CanRenameLayout()
        => Manager.CanRenameCurrentLayout;

    [RelayCommand(CanExecute = nameof(CanAssignCamera))]
    private void AssignCamera()
        => Manager.AssignCameraToLayout();

    private bool CanAssignCamera()
        => Manager.CanAssignCameraToLayout;

    [RelayCommand(CanExecute = nameof(CanDeleteLayout))]
    private void DeleteLayout()
        => Manager.DeleteCurrentLayout();

    private bool CanDeleteLayout()
        => Manager.CanDeleteCurrentLayout;

    [RelayCommand(CanExecute = nameof(CanSetAsStartup))]
    private void SetAsStartup()
        => Manager.SetCurrentAsStartup();

    private bool CanSetAsStartup()
        => Manager.CanSetCurrentAsStartup;

    [RelayCommand(CanExecute = nameof(CanReconnectAll))]
    private void ReconnectAll()
        => Manager.ReconnectAll();

    private bool CanReconnectAll()
        => Manager.CanReconnectAll;

    [RelayCommand]
    private void ShowAbout()
        => Manager.ShowAboutDialog();

    [RelayCommand]
    private void CheckForUpdates()
        => Manager.ShowCheckForUpdatesDialog();

    [RelayCommand]
    private void ShowSettings()
        => Manager.ShowSettingsDialog();

    [RelayCommand]
    private void ShowRecordingsBrowser()
        => Manager.ShowRecordingsBrowserDialog();

    [RelayCommand]
    private void ShowTimeline()
        => Manager.ShowTimeline();

    /// <summary>
    /// Switches to the layout in the given place of the layout list
    /// (Ctrl+1 to Ctrl+9). A number without a layout does nothing.
    /// </summary>
    [RelayCommand]
    private void SelectLayout(string? number)
    {
        if (LayoutQuickSelect.GetByNumber(Manager.Layouts, number) is { } layout)
        {
            Manager.CurrentLayout = layout;
        }
    }

    [RelayCommand]
    private void ShowKeyboardShortcuts()
        => dialogService.ShowInfo(Translations.KeyboardShortcutsText, Translations.KeyboardShortcuts);

    [RelayCommand]
    private void DownloadUpdate()
        => Manager.DownloadLatestUpdate();

    [RelayCommand]
    private static void Exit()
        => Application.Current.Shutdown();
}