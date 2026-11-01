using IUsbCameraWatcher = AtcSoft.Video.Surveillance.Services.IUsbCameraWatcher;
using UsbCameraEventArgs = AtcSoft.Video.Surveillance.Events.UsbCameraEventArgs;

namespace AtcSoft.Video.Surveillance.Wpf.ViewModels;

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
    private readonly AtcSoft.Video.Surveillance.Services.IApplicationSettingsService? settingsService;
    private readonly ILoggerFactory loggerFactory;
    private readonly Lock loadLock = new();
    private bool disposed;
    private bool loadedOnce;
    private bool loading;
    private bool reloadWanted;
    private bool usbWatcherSubscribed;
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
    [ObservableProperty(AfterChangedCallback = nameof(OnArrangementInputChanged), DependentPropertyNames = [nameof(SelectedGridPresetIndex)])]
    private string selectedGridPreset = "0";

    [ObservableProperty]
    private string pageLabel = string.Empty;

    [ObservableProperty]
    private bool hasPages;

    [ObservableProperty]
    private bool hasNoMatches;

    private int pageIndex;

    /// <summary>
    /// Gets or sets the grid's size as the place of its segment: 0 is automatic, then one to four cameras along a side.
    /// </summary>
    public int SelectedGridPresetIndex
    {
        get => (int)GridPager.ParsePreset(SelectedGridPreset);
        set
        {
            if (value >= 0)
            {
                SelectedGridPreset = value.ToString(CultureInfo.InvariantCulture);
            }
        }
    }

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
        AtcSoft.Video.Surveillance.Services.IApplicationSettingsService? settingsService = null,
        ILoggerFactory? loggerFactory = null)
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
        this.loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;

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
        if (disposed || !loadedOnce)
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

    /// <summary>
    /// Loads the cameras: one load at a time. A load asked for while one runs
    /// (the view opened, F5, missed events) runs once more after it.
    /// </summary>
    [RelayCommand("Load")]
    private async Task LoadAsync()
    {
        lock (loadLock)
        {
            loadedOnce = true;
            if (loading)
            {
                reloadWanted = true;
                return;
            }

            loading = true;
        }

        var loaded = false;
        try
        {
            while (true)
            {
                await LoadOnceAsync().ConfigureAwait(false);

                lock (loadLock)
                {
                    if (!reloadWanted || disposed)
                    {
                        loading = false;
                        loaded = true;
                        return;
                    }

                    reloadWanted = false;
                }
            }
        }
        finally
        {
            if (!loaded)
            {
                lock (loadLock)
                {
                    loading = false;
                    reloadWanted = false;
                }
            }
        }
    }

    // Brings the tiles in line with the server's cameras: a camera that stays
    // keeps its tile, its player and its stream (only its lease is checked),
    // a new one gets a tile, and one that went has its tile closed off the UI
    // thread. A full screen window of a camera that stays goes on playing.
    private async Task LoadOnceAsync()
    {
        await OnUiAsync(() => IsLoading = true).ConfigureAwait(false);

        try
        {
            var cameras = await gatewayService
                .GetCamerasAsync()
                .ConfigureAwait(false);

            // The server could not be reached: the wall stays as it is, and its
            // tiles start their streams again by themselves.
            if (cameras is null || disposed)
            {
                return;
            }

            var showConnectionTypeIcon = await IsConnectionTypeIconEnabledAsync().ConfigureAwait(false);

            var added = new List<CameraTileViewModel>();
            var kept = new List<CameraTileViewModel>();
            var removed = new List<CameraTileViewModel>();
            await OnUiAsync(() =>
            {
                var wanted = cameras.Select(camera => camera.Id).ToHashSet();
                foreach (var tile in CameraTiles.Where(tile => !wanted.Contains(tile.CameraId)).ToList())
                {
                    CameraTiles.Remove(tile);
                    removed.Add(tile);
                }

                for (var index = 0; index < cameras.Length; index++)
                {
                    var camera = cameras[index];
                    var tile = CameraTiles.FirstOrDefault(t => t.CameraId == camera.Id);
                    if (tile is null)
                    {
                        tile = new CameraTileViewModel(videoPlayerFactory, gatewayService, eventService, apiBaseAddress, logger: loggerFactory.CreateLogger<CameraTileViewModel>())
                        {
                            CameraId = camera.Id,
                        };
                        CameraTiles.Insert(index, tile);
                        added.Add(tile);
                    }
                    else
                    {
                        var at = CameraTiles.IndexOf(tile);
                        if (at != index)
                        {
                            CameraTiles.Move(at, index);
                        }

                        kept.Add(tile);
                    }

                    tile.DisplayName = camera.DisplayName;
                    tile.Description = camera.Description ?? string.Empty;
                    tile.ConnectionState = HubStates.ToConnectionState(camera.ConnectionState);
                    tile.IsRecording = camera.IsRecording;
                    tile.UsbDeviceId = camera.UsbDeviceId ?? string.Empty;
                    tile.ConnectionType = camera.ConnectionType.ToCoreConnectionType();
                    tile.ShowConnectionTypeIcon = showConnectionTypeIcon;
                    tile.Rotation = camera.Rotation.ToCoreRotation();
                }

                // Size the grid, and leave out what the filter and the page leave out
                ApplyArrangement(wrapPages: false);
            }).ConfigureAwait(false);

            // Closing a player waits for its reader: off the UI thread.
            if (removed.Count > 0)
            {
                await Task.Run(() => removed.ForEach(tile => tile.Dispose())).ConfigureAwait(false);
            }

            // A tile whose stream cannot start (the server refused it, timed
            // out or could not be reached) tries again by itself; it never
            // holds up the other tiles. One that stays checks its lease: a
            // server that restarted no longer has it, and the stream starts again.
            foreach (var tile in kept)
            {
                await tile.EnsureStreamAsync().ConfigureAwait(false);
            }

            foreach (var tile in added)
            {
                await tile.StartStreamOrRetryLaterAsync().ConfigureAwait(false);
            }
        }
        catch (HttpRequestException)
        {
            // The server could not be reached: the wall stays as it is.
        }
        finally
        {
            await OnUiAsync(() => IsLoading = false).ConfigureAwait(false);
        }
    }

    // Runs on the UI thread, or at once where there is none (tests, or the app
    // closing).
    private static Task OnUiAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
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

        // Emptied on the UI thread; the players are closed off it.
        var removed = new List<CameraTileViewModel>();
        await OnUiAsync(() =>
        {
            removed.AddRange(CameraTiles);
            CameraTiles.Clear();
        }).ConfigureAwait(false);
        await Task.Run(() => removed.ForEach(tile => tile.Dispose())).ConfigureAwait(false);
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

        // The window borrows the tile's player: when the tile goes (its camera
        // was removed, or all streams were stopped), the window goes with it.
        var following = WhenTileCloses(tile, () => _ = window.Dispatcher.InvokeAsync(window.Close));
        window.Closed += (_, _) => following.Dispose();
        window.Show();
    }

    /// <summary>
    /// Calls <paramref name="onClosed"/> once, when the tile's player is gone
    /// (the tile was disposed). Dispose the result to stop following.
    /// </summary>
    internal static IDisposable WhenTileCloses(
        CameraTileViewModel tile,
        Action onClosed)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(onClosed);

        void OnTilePropertyChanged(
            object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(CameraTileViewModel.Player), StringComparison.Ordinal) && tile.Player is null)
            {
                tile.PropertyChanged -= OnTilePropertyChanged;
                onClosed();
            }
        }

        tile.PropertyChanged += OnTilePropertyChanged;
        return new Unsubscriber(() => tile.PropertyChanged -= OnTilePropertyChanged);
    }

    private sealed class Unsubscriber(Action unsubscribe) : IDisposable
    {
        public void Dispose()
            => unsubscribe();
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
}