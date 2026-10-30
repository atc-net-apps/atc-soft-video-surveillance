namespace AtcSoft.VideoSurveillance.Wpf.App;

/// <summary>
/// View model for the main window.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly GatewayService gatewayService;
    private readonly SurveillanceEventService eventService;
    private readonly AuthService authService;
    private readonly SignInFlow signInFlow;
    private readonly ClientSession session;
    private readonly IGitHubReleaseService gitHubReleaseService;
    private readonly IApplicationSettingsService settingsService;
    private readonly IAutoStartService autoStartService;
    private readonly IDiagnosticsExportService diagnosticsExportService;
    private readonly LiveViewViewModel liveViewViewModel;
    private readonly DashboardViewModel dashboardViewModel;
    private readonly CameraListViewModel cameraListViewModel;
    private readonly LayoutListViewModel layoutListViewModel;
    private readonly RecordingsViewModel recordingsViewModel;
    private readonly TimelineViewModel timelineViewModel;
    private readonly NotificationHistoryViewModel notificationHistoryViewModel;

    private WindowState previousWindowState = WindowState.Normal;
    private WindowStyle previousWindowStyle = WindowStyle.SingleBorderWindow;
    private ResizeMode previousResizeMode = ResizeMode.CanResize;
    private DispatcherTimer? latencyTimer;
    private bool suppressAutoStartCallback;

    [ObservableProperty]
    private string statusText = Translations.Ready;

    [ObservableProperty]
    private string serverUrl;

    [ObservableProperty]
    private string liveUpdatesText = HubStates.ServerText(connected: false);

    [ObservableProperty(DependentPropertyNames = [nameof(ShowChangeServer)])]
    private bool isAspireManaged;

    [ObservableProperty]
    private ViewModelBase? currentView;

    [ObservableProperty]
    private bool isFullScreen;

    [ObservableProperty]
    private int connectedCameras;

    [ObservableProperty]
    private int activeRecordings;

    [ObservableProperty]
    private string serverLatency = "--";

    // The server's disk with the recordings ("120 GB free of 500 GB").
    [ObservableProperty]
    private string serverDiskSpace = "--";

    [ObservableProperty]
    private bool isServerDiskSpaceLow;

    [ObservableProperty(AfterChangedCallback = nameof(OnStartWithWindowsChanged))]
    private bool startWithWindows;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    public MainWindowViewModel(
        GatewayService gatewayService,
        SurveillanceEventService eventService,
        AuthService authService,
        SignInFlow signInFlow,
        ClientSession session,
        IGitHubReleaseService gitHubReleaseService,
        IApplicationSettingsService settingsService,
        IAutoStartService autoStartService,
        IDiagnosticsExportService diagnosticsExportService,
        LiveViewViewModel liveViewViewModel,
        DashboardViewModel dashboardViewModel,
        CameraListViewModel cameraListViewModel,
        LayoutListViewModel layoutListViewModel,
        RecordingsViewModel recordingsViewModel,
        TimelineViewModel timelineViewModel,
        NotificationHistoryViewModel notificationHistoryViewModel,
        string apiBaseAddress,
        bool isAspireManaged)
    {
        ArgumentNullException.ThrowIfNull(gatewayService);
        ArgumentNullException.ThrowIfNull(eventService);
        ArgumentNullException.ThrowIfNull(authService);
        ArgumentNullException.ThrowIfNull(signInFlow);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(gitHubReleaseService);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(autoStartService);
        ArgumentNullException.ThrowIfNull(diagnosticsExportService);
        ArgumentNullException.ThrowIfNull(liveViewViewModel);
        ArgumentNullException.ThrowIfNull(dashboardViewModel);
        ArgumentNullException.ThrowIfNull(cameraListViewModel);
        ArgumentNullException.ThrowIfNull(layoutListViewModel);
        ArgumentNullException.ThrowIfNull(recordingsViewModel);
        ArgumentNullException.ThrowIfNull(timelineViewModel);
        ArgumentNullException.ThrowIfNull(notificationHistoryViewModel);

        this.gatewayService = gatewayService;
        this.eventService = eventService;
        this.authService = authService;
        this.signInFlow = signInFlow;
        this.session = session;
        this.gitHubReleaseService = gitHubReleaseService;
        this.settingsService = settingsService;
        this.autoStartService = autoStartService;
        this.diagnosticsExportService = diagnosticsExportService;
        this.liveViewViewModel = liveViewViewModel;
        this.dashboardViewModel = dashboardViewModel;
        this.cameraListViewModel = cameraListViewModel;
        this.layoutListViewModel = layoutListViewModel;
        this.recordingsViewModel = recordingsViewModel;
        this.timelineViewModel = timelineViewModel;
        this.notificationHistoryViewModel = notificationHistoryViewModel;
        serverUrl = apiBaseAddress;
        this.isAspireManaged = isAspireManaged;

        // Seed from the registry. Use the suppression flag so the
        // AfterChangedCallback doesn't write back during construction —
        // that would be a redundant registry round-trip and would mask
        // a future bug where the seed disagreed with the registry state.
        suppressAutoStartCallback = true;
        StartWithWindows = autoStartService.IsEnabled;
        suppressAutoStartCallback = false;

        // Default view
        CurrentView = liveViewViewModel;

        this.eventService.ConnectedChanged += () =>
        {
            _ = Application.Current?.Dispatcher.InvokeAsync(() =>
                LiveUpdatesText = HubStates.ServerText(this.eventService.IsConnected));
        };

        this.eventService.OnConnectionStateChanged += OnConnectionStateChanged;

        // Another user, or the same one with a new role after a renewal.
        this.session.Changed += () =>
        {
            _ = Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                RaisePropertyChanged(nameof(SignedInText));
                RaisePropertyChanged(nameof(ShowManageUsers));
            });
        };
        this.eventService.OnRecordingStateChanged += OnRecordingStateChanged;

        // The language changes live; CultureManager holds the handler weakly.
        CultureManager.UiCultureChanged += OnUiCultureChanged;
    }

    private void OnUiCultureChanged(
        object? sender,
        UiCultureEventArgs e)
        => LiveUpdatesText = HubStates.ServerText(eventService.IsConnected);

    [RelayCommand]
    private void ViewLiveView()
    {
        CurrentView = liveViewViewModel;
        liveViewViewModel.LoadCommand.Execute(parameter: null);
    }

    [RelayCommand]
    private void ViewDashboard()
    {
        CurrentView = dashboardViewModel;
        dashboardViewModel.LoadCommand.Execute(parameter: null);
    }

    [RelayCommand]
    private void ViewCameras()
    {
        CurrentView = cameraListViewModel;
        cameraListViewModel.LoadCommand.Execute(parameter: null);
    }

    [RelayCommand]
    private void ViewLayouts()
    {
        CurrentView = layoutListViewModel;
        layoutListViewModel.LoadCommand.Execute(parameter: null);
    }

    [RelayCommand]
    private void ViewRecordings()
    {
        CurrentView = recordingsViewModel;
        recordingsViewModel.LoadCommand.Execute(parameter: null);
    }

    [RelayCommand]
    private void ViewTimeline()
    {
        CurrentView = timelineViewModel;
        timelineViewModel.LoadCommand.Execute(parameter: null);
    }

    [RelayCommand]
    private void ViewNotifications()
        => CurrentView = notificationHistoryViewModel;

    /// <summary>
    /// Gets who is signed in, for the backstage.
    /// </summary>
    public string SignedInText
        => string.Format(CultureInfo.CurrentCulture, Translations.SignedInAs1, session.UserName);

    [RelayCommand]
    private void ChangePassword()
    {
        var viewModel = new AtcSoft.VideoSurveillance.Wpf.Dialogs.ChangePasswordDialogViewModel(authService.ChangeOwnPasswordAsync);
        var dialog = new AtcSoft.VideoSurveillance.Wpf.Dialogs.ChangePasswordDialog(viewModel)
        {
            Owner = Application.Current.MainWindow,
        };

        dialog.ShowDialog();
    }

    /// <summary>
    /// Gets whether the users dialog is offered: to an admin only.
    /// </summary>
    public Visibility ShowManageUsers
        => session.IsAdmin ? Visibility.Visible : Visibility.Collapsed;

    [RelayCommand]
    private void ManageUsers()
        => AtcSoft.VideoSurveillance.Wpf.Dialogs.UsersDialog.Show(gatewayService, Application.Current.MainWindow);

    // Signed out, the client signs in again (as someone else, perhaps) or closes.
    [RelayCommand("SignOut")]
    private async Task SignOutAsync()
    {
        await authService.SignOutAsync().ConfigureAwait(true);
        if (!await signInFlow.ShowDialogAsync(Application.Current.MainWindow).ConfigureAwait(true))
        {
            Application.Current.Shutdown();
        }
    }

    [RelayCommand("ShowSettings")]
    private async Task ShowSettingsAsync()
    {
        var viewModel = new AtcSoft.VideoSurveillance.Wpf.Dialogs.SettingsDialogViewModel(gatewayService, settingsService);
        await viewModel.LoadSettingsAsync().ConfigureAwait(true);

        var dialog = new AtcSoft.VideoSurveillance.Wpf.Dialogs.SettingsDialog(viewModel)
        {
            Owner = Application.Current.MainWindow,
        };

        dialog.ShowDialog();
    }

    [RelayCommand]
    private static void ShowAbout()
    {
        var version = ApplicationHelper.GetVersion();
        var year = DateTime.Now.Year;

        var dialog = new AboutDialog(version, year)
        {
            Owner = Application.Current.MainWindow,
        };

        dialog.ShowDialog();
    }

    [RelayCommand]
    private void ShowCheckForUpdates()
    {
        var viewModel = new CheckForUpdatesDialogViewModel(gitHubReleaseService);

        var dialog = new CheckForUpdatesDialog(viewModel)
        {
            Owner = Application.Current.MainWindow,
        };

        dialog.ShowDialog();
    }

    [RelayCommand("ExportDiagnostics")]
    private async Task ExportDiagnosticsAsync()
    {
        var defaultFileName = $"VideoSurveillance-Diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.json";
        var dialog = new SaveFileDialog
        {
            Title = Translations.ExportDiagnostics,
            FileName = defaultFileName,
            DefaultExt = ".json",
            Filter = Translations.JsonFileFilter,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            OverwritePrompt = true,
        };

        // Owner-relative dialog so it lands over the main window instead
        // of jumping to a random monitor.
        var owner = Application.Current.MainWindow;
        var result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        if (result != true)
        {
            return;
        }

        try
        {
            var report = await diagnosticsExportService
                .BuildReportAsync()
                .ConfigureAwait(true);
            await diagnosticsExportService
                .WriteAsync(dialog.FileName, report)
                .ConfigureAwait(true);

            StatusText = string.Format(CultureInfo.CurrentCulture, Translations.DiagnosticsExportedTo1, dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = string.Format(CultureInfo.CurrentCulture, Translations.DiagnosticsExportFailed1, ex.Message);
        }
    }

    /// <summary>
    /// Gets whether the "Change Server" button should be visible.
    /// </summary>
    public Visibility ShowChangeServer
        => IsAspireManaged ? Visibility.Collapsed : Visibility.Visible;

    [RelayCommand]
    private static void ChangeServer()
    {
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath))
        {
            Process.Start(exePath, "--choose-server");
            Application.Current.Shutdown();
        }
    }

    [RelayCommand]
    private static void Exit()
        => Application.Current.Shutdown();

    private void OnStartWithWindowsChanged()
    {
        if (suppressAutoStartCallback)
        {
            return;
        }

        if (StartWithWindows)
        {
            autoStartService.Enable();
        }
        else
        {
            autoStartService.Disable();
        }
    }

    [RelayCommand]
    private void ToggleFullScreen()
    {
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow is null)
        {
            return;
        }

        if (IsFullScreen)
        {
            RestoreFromFullScreen(mainWindow);
        }
        else
        {
            previousWindowState = mainWindow.WindowState;
            previousWindowStyle = mainWindow.WindowStyle;
            previousResizeMode = mainWindow.ResizeMode;

            mainWindow.WindowStyle = WindowStyle.None;
            mainWindow.ResizeMode = ResizeMode.NoResize;
            mainWindow.WindowState = WindowState.Maximized;
            IsFullScreen = true;
        }
    }

    [RelayCommand]
    [SuppressMessage("Design", "S1871", Justification = "Each case calls LoadCommand on a different type — no shared interface")]
    private void RefreshCurrentView()
    {
        switch (CurrentView)
        {
            case LiveViewViewModel vm:
                vm.LoadCommand.Execute(parameter: null);
                break;
            case DashboardViewModel vm:
                vm.LoadCommand.Execute(parameter: null);
                break;
            case CameraListViewModel vm:
                vm.LoadCommand.Execute(parameter: null);
                break;
            case LayoutListViewModel vm:
                vm.LoadCommand.Execute(parameter: null);
                break;
            case RecordingsViewModel vm:
                vm.LoadCommand.Execute(parameter: null);
                break;
            case TimelineViewModel vm:
                vm.LoadCommand.Execute(parameter: null);
                break;
        }
    }

    [RelayCommand]
    private static void ShowKeyboardShortcuts()
    {
        var dialog = new Dialogs.KeyboardShortcutsDialog
        {
            Owner = Application.Current.MainWindow,
        };

        dialog.ShowDialog();
    }

    /// <summary>
    /// Restores the window from full-screen mode.
    /// </summary>
    internal void RestoreFromFullScreen(Window window)
    {
        window.WindowStyle = previousWindowStyle;
        window.ResizeMode = previousResizeMode;
        window.WindowState = previousWindowState;
        IsFullScreen = false;
    }

    /// <summary>
    /// Loads initial camera and recording stats from the API.
    /// </summary>
    public async Task LoadInitialStatsAsync()
    {
        try
        {
            var cameras = await gatewayService
                .GetCamerasAsync()
                .ConfigureAwait(true);

            if (cameras is not null)
            {
                ConnectedCameras = cameras.Count(c =>
                    c.ConnectionState == CameraConnectionState.Connected);
                ActiveRecordings = cameras.Count(c => c.IsRecording);
            }
        }
        catch (HttpRequestException)
        {
            ConnectedCameras = 0;
            ActiveRecordings = 0;
        }
    }

    /// <summary>
    /// Starts periodic latency monitoring (every 30 seconds).
    /// </summary>
    public void StartLatencyMonitoring()
    {
        latencyTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30),
        };
        latencyTimer.Tick += async (_, _) => await MeasureLatencyAsync().ConfigureAwait(false);
        latencyTimer.Start();

        // Fire initial measurement
        _ = MeasureLatencyAsync();
    }

    /// <summary>
    /// Stops the latency monitoring timer.
    /// </summary>
    public void StopLatencyMonitoring()
    {
        latencyTimer?.Stop();
        latencyTimer = null;
    }

    private async Task MeasureLatencyAsync()
    {
        try
        {
            var sw = Stopwatch.StartNew();
            await gatewayService.GetSettingsAsync().ConfigureAwait(false);
            sw.Stop();

            // Asked with the same beat: recordings stop when the disk runs out.
            var storage = await gatewayService.GetStorageStatusAsync().ConfigureAwait(false);

            _ = Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                ServerLatency = $"{sw.ElapsedMilliseconds} ms";
                ServerDiskSpace = storage?.Text ?? "N/A";
                IsServerDiskSpaceLow = storage?.IsLow ?? false;
            });
        }
        catch
        {
            _ = Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                ServerLatency = "N/A";
                ServerDiskSpace = "N/A";
                IsServerDiskSpaceLow = false;
            });
        }
    }

    private void OnConnectionStateChanged(ConnectionStateChangedEvent e)
    {
        _ = Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (e.NewState == CameraConnectionState.Connected)
            {
                ConnectedCameras++;
            }
            else
            {
                ConnectedCameras = Math.Max(0, ConnectedCameras - 1);
            }
        });
    }

    private void OnRecordingStateChanged(RecordingStateChangedEvent e)
    {
        _ = Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (HubStates.IsRecording(e.NewState) && !HubStates.IsRecording(e.OldState))
            {
                ActiveRecordings++;
            }
            else if (!HubStates.IsRecording(e.NewState) && HubStates.IsRecording(e.OldState))
            {
                ActiveRecordings = Math.Max(0, ActiveRecordings - 1);
            }
        });
    }
}