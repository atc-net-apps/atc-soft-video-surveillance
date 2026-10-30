using IUsbCameraWatcher = AtcSoft.VideoSurveillance.Services.IUsbCameraWatcher;
using UsbCameraEventArgs = AtcSoft.VideoSurveillance.Events.UsbCameraEventArgs;

namespace AtcSoft.VideoSurveillance.Wpf.ViewModels;

/// <summary>
/// View model for the live camera grid view.
/// Manages camera tiles, grid layout, and stream lifecycle.
/// </summary>
public sealed partial class LiveViewViewModel : ViewModelBase, IDisposable
{
    private readonly GatewayService gatewayService;
    private readonly SurveillanceEventService eventService;
    private readonly IVideoPlayerFactory videoPlayerFactory;
    private readonly IUsbCameraWatcher usbCameraWatcher;
    private readonly string apiBaseAddress;
    private readonly AtcSoft.VideoSurveillance.Services.IApplicationSettingsService? settingsService;
    private bool disposed;
    private bool loadedOnce;
    private bool usbWatcherSubscribed;
    private Dictionary<string, string> gridPresetItems = BuildGridPresetItems();

    [ObservableProperty]
    private ObservableCollection<CameraTileViewModel> cameraTiles = [];

    [ObservableProperty]
    private int gridRows;

    [ObservableProperty]
    private int gridColumns;

    [ObservableProperty]
    private bool isLoading;

    // What is typed in the filter box: the cameras whose name or
    // description has every word of it are shown.
    [ObservableProperty(AfterChangedCallback = nameof(OnArrangementInputChanged))]
    private string filterText = string.Empty;

    // The grid's size, as its number of cameras along a side ("0" is automatic).
    [ObservableProperty(AfterChangedCallback = nameof(OnArrangementInputChanged))]
    private string selectedGridPreset = "0";

    [ObservableProperty]
    private string pageLabel = string.Empty;

    [ObservableProperty]
    private bool hasPages;

    [ObservableProperty]
    private bool hasNoMatches;

    private int pageIndex;

    /// <summary>
    /// Gets the grid sizes to choose from.
    /// </summary>
    public IDictionary<string, string> GridPresetItems
        => gridPresetItems;

    private static Dictionary<string, string> BuildGridPresetItems()
        => new(StringComparer.Ordinal)
    {
        ["0"] = Translations.GridSizeAuto,
        ["1"] = "1 \u00D7 1",
        ["2"] = "2 \u00D7 2",
        ["3"] = "3 \u00D7 3",
        ["4"] = "4 \u00D7 4",
    };

    /// <summary>
    /// Shows the grid sizes in the language just chosen.
    /// </summary>
    internal void RefreshTexts()
    {
        gridPresetItems = BuildGridPresetItems();
        OnPropertyChanged(nameof(GridPresetItems));
    }

    private void OnUiCultureChanged(
        object? sender,
        UiCultureEventArgs e)
        => RefreshTexts();

    [RelayCommand]
    private void ClearFilter()
        => FilterText = string.Empty;

    [RelayCommand(CanExecute = nameof(HasPages))]
    private void NextPage()
    {
        pageIndex++;
        ApplyArrangement(wrapPages: true);
    }

    [RelayCommand(CanExecute = nameof(HasPages))]
    private void PreviousPage()
    {
        pageIndex--;
        ApplyArrangement(wrapPages: true);
    }

    // A new filter or grid size starts at the first page again.
    private void OnArrangementInputChanged()
    {
        pageIndex = 0;
        ApplyArrangement(wrapPages: false);
        RememberGridSize();
    }

    private void RememberGridSize()
    {
        if (settingsService is null)
        {
            return;
        }

        var size = (int)GridPager.ParsePreset(SelectedGridPreset);
        if (settingsService.General.GridSize == size)
        {
            return;
        }

        var general = settingsService.General.Clone();
        general.GridSize = size;
        settingsService.SaveGeneral(general);
    }

    /// <summary>
    /// Shows the tiles the filter and the page leave in, and sizes the grid
    /// for them. Has to run on the UI thread.
    /// </summary>
    private void ApplyArrangement(bool wrapPages)
    {
        var cameras = CameraTiles.Select(tile => (tile.DisplayName, tile.Description)).ToList();
        var preset = GridPager.ParsePreset(SelectedGridPreset);
        var arrangement = LiveGridArrangement.Arrange(cameras, FilterText, preset, pageIndex);

        // Past the last page comes the first, and before the first the last.
        if (wrapPages && arrangement.PageCount > 1 && arrangement.PageIndex != pageIndex)
        {
            pageIndex = pageIndex < 0 ? arrangement.PageCount - 1 : 0;
            arrangement = LiveGridArrangement.Arrange(cameras, FilterText, preset, pageIndex);
        }

        pageIndex = arrangement.PageIndex;

        for (var i = 0; i < CameraTiles.Count; i++)
        {
            CameraTiles[i].IsShownInGrid = arrangement.Shown[i];
        }

        GridRows = arrangement.Rows;
        GridColumns = arrangement.Columns;
        PageLabel = arrangement.PageLabel;
        HasPages = arrangement.PageCount > 1;
        HasNoMatches = CameraTiles.Count > 0 && arrangement.MatchCount == 0;
        CommandManager.InvalidateRequerySuggested();
    }

    public LiveViewViewModel(
        GatewayService gatewayService,
        SurveillanceEventService eventService,
        IVideoPlayerFactory videoPlayerFactory,
        IUsbCameraWatcher usbCameraWatcher,
        string apiBaseAddress,
        AtcSoft.VideoSurveillance.Services.IApplicationSettingsService? settingsService = null)
    {
        ArgumentNullException.ThrowIfNull(gatewayService);
        ArgumentNullException.ThrowIfNull(eventService);
        ArgumentNullException.ThrowIfNull(videoPlayerFactory);
        ArgumentNullException.ThrowIfNull(usbCameraWatcher);

        this.gatewayService = gatewayService;
        this.eventService = eventService;
        this.videoPlayerFactory = videoPlayerFactory;
        this.usbCameraWatcher = usbCameraWatcher;
        this.apiBaseAddress = apiBaseAddress;
        this.settingsService = settingsService;

        // The language changes live; CultureManager holds the handler weakly.
        CultureManager.UiCultureChanged += OnUiCultureChanged;

        // The grid starts in the size it was last shown in.
        selectedGridPreset = ((int)GridPager.ParsePreset(
            settingsService?.General.GridSize.ToString(CultureInfo.InvariantCulture))).ToString(CultureInfo.InvariantCulture);

        // Subscribe immediately so unplug events that arrive before the
        // first LoadAsync (e.g. events the server sends again after a drop) are still
        // applied once the matching tile materializes.
        SubscribeToUsbWatcher();

        eventService.OnMissedEvents += OnMissedEvents;
    }

    // The server was away (it may have restarted), and cameras may have been
    // added or removed meanwhile: the view is loaded again. A view nobody has
    // opened yet stays unloaded.
    private void OnMissedEvents()
    {
        if (disposed || !loadedOnce || IsLoading)
        {
            return;
        }

        _ = LoadAsync();
    }

    private void SubscribeToUsbWatcher()
    {
        if (usbWatcherSubscribed)
        {
            return;
        }

        usbCameraWatcher.DeviceArrived += OnUsbDeviceArrived;
        usbCameraWatcher.DeviceRemoved += OnUsbDeviceRemoved;
        usbCameraWatcher.Start();
        usbWatcherSubscribed = true;
    }

    private void OnUsbDeviceArrived(
        object? sender,
        UsbCameraEventArgs e)
        => UpdateTileUnpluggedState(e.Device.DeviceId, unplugged: false);

    private void OnUsbDeviceRemoved(
        object? sender,
        UsbCameraEventArgs e)
        => UpdateTileUnpluggedState(e.Device.DeviceId, unplugged: true);

    private void UpdateTileUnpluggedState(
        string deviceId,
        bool unplugged)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return;
        }

        // RemoteUsbCameraWatcher forwards event payloads on whatever
        // thread the hub fires them on — marshal to the UI dispatcher
        // before mutating observable properties bound to the WPF view.
        // When there's no Application.Current (unit-test scenario) run
        // synchronously on the current thread — there's no UI binding
        // to violate and the test setup is single-threaded anyway.
        var app = Application.Current;
        if (app is null)
        {
            ApplyTileUnpluggedState(deviceId, unplugged);
            return;
        }

        _ = app.Dispatcher.BeginInvoke(() => ApplyTileUnpluggedState(deviceId, unplugged));
    }

    private void ApplyTileUnpluggedState(
        string deviceId,
        bool unplugged)
    {
        foreach (var tile in CameraTiles)
        {
            if (string.Equals(tile.UsbDeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                tile.IsDeviceUnplugged = unplugged;
            }
        }
    }

    /// <summary>
    /// Reads the connection-type icon toggle from the server settings. A failure
    /// only hides the icon; it must never block the live view.
    /// </summary>
    private async Task<bool> IsConnectionTypeIconEnabledAsync()
    {
        try
        {
            var settings = await gatewayService
                .GetSettingsAsync()
                .ConfigureAwait(false);
            return settings?.ShowOverlayConnectionTypeIcon ?? false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    [RelayCommand("Load")]
    private async Task LoadAsync()
    {
        loadedOnce = true;
        IsLoading = true;

        try
        {
            var cameras = await gatewayService
                .GetCamerasAsync()
                .ConfigureAwait(false);

            var showConnectionTypeIcon = await IsConnectionTypeIconEnabledAsync().ConfigureAwait(false);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Stop & dispose existing tiles
                StopAndDisposeTiles();

                if (cameras is null || cameras.Length == 0)
                {
                    GridRows = 0;
                    GridColumns = 0;
                    return;
                }

                // Create a tile per camera
                foreach (var camera in cameras)
                {
                    var tile = new CameraTileViewModel(videoPlayerFactory, gatewayService, eventService, apiBaseAddress)
                    {
                        CameraId = camera.Id,
                        DisplayName = camera.DisplayName,
                        Description = camera.Description ?? string.Empty,
                        ConnectionState = HubStates.ToConnectionState(camera.ConnectionState),
                        IsRecording = camera.IsRecording,
                        UsbDeviceId = camera.UsbDeviceId ?? string.Empty,
                        ConnectionType = camera.ConnectionType.ToCoreConnectionType(),
                        ShowConnectionTypeIcon = showConnectionTypeIcon,
                        Rotation = camera.Rotation.ToCoreRotation(),
                    };

                    CameraTiles.Add(tile);
                }

                // Size the grid, and leave out what the filter and the page leave out
                ApplyArrangement(wrapPages: false);
            });

            // Start all streams (off UI thread)
            var tiles = await Application.Current.Dispatcher.InvokeAsync(() => CameraTiles.ToList());
            foreach (var tile in tiles)
            {
                try
                {
                    await tile.StartStreamAsync().ConfigureAwait(false);
                }
                catch (HttpRequestException)
                {
                    // Per-tile stream start failed (the server refused it, with
                    // its reason, or could not be reached) — the tile shows
                    // "No Stream". A single tile failing must not abort the rest
                    // of the live-view load.
                }
            }
        }
        catch (HttpRequestException)
        {
            var app = Application.Current;
            if (app is not null)
            {
                await app.Dispatcher.InvokeAsync(() =>
                {
                    StopAndDisposeTiles();
                    GridRows = 0;
                    GridColumns = 0;
                });
            }
        }
        finally
        {
            // Application.Current can be null if a fatal exception triggered
            // App.ApplicationOnDispatcherUnhandledException → Shutdown while
            // this finally is unwinding. The app is going away anyway, so a
            // missed IsLoading = false is harmless.
            var app = Application.Current;
            if (app is not null)
            {
                await app.Dispatcher.InvokeAsync(() => IsLoading = false);
            }
        }
    }

    /// <summary>
    /// Gives every tile's stream lease back at once, so the server stops the
    /// streams now instead of when the leases run out (the app closes).
    /// </summary>
    public Task ReleaseStreamsAsync()
        => CameraTileViewModel.StopAllAsync([.. CameraTiles]);

    [RelayCommand("StopAll")]
    private async Task StopAllAsync()
    {
        var tiles = await Application.Current.Dispatcher.InvokeAsync(() => CameraTiles.ToList());

        foreach (var tile in tiles)
        {
            try
            {
                await tile.StopStreamAsync().ConfigureAwait(false);
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        await Application.Current.Dispatcher.InvokeAsync(StopAndDisposeTiles);
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Window self-disposes on close via the Closed event handler.")]
    public void OpenFullScreen(CameraTileViewModel tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        var viewModel = new FullScreenCameraWindowViewModel(
            eventService,
            tile.CameraId,
            tile.Player,
            tile.DisplayName,
            tile.Description,
            tile.Rotation);

        var window = new FullScreenCameraWindow(viewModel);
        window.Show();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        eventService.OnMissedEvents -= OnMissedEvents;

        if (usbWatcherSubscribed)
        {
            usbCameraWatcher.DeviceArrived -= OnUsbDeviceArrived;
            usbCameraWatcher.DeviceRemoved -= OnUsbDeviceRemoved;
            usbCameraWatcher.Stop();
            usbWatcherSubscribed = false;
        }

        // DI scope teardown at app shutdown calls Dispose on a worker
        // thread, so mutating CameraTiles (which is bound to a WPF
        // CollectionView) would throw NotSupportedException —
        // CollectionViews require dispatcher-thread mutation. Tile
        // disposal itself is non-UI work and safe from any thread; the
        // bound collection is going away with the app, so we skip the
        // cosmetic emptying of it on this path.
        foreach (var tile in CameraTiles)
        {
            tile.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private void StopAndDisposeTiles()
    {
        foreach (var tile in CameraTiles)
        {
            tile.Dispose();
        }

        CameraTiles.Clear();
    }
}