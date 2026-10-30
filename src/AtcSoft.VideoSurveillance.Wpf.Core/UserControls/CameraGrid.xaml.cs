namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

/// <summary>
/// Control for displaying multiple camera tiles in a dynamic grid layout.
/// </summary>
/// <remarks>
/// CS0169 / CS0414 are suppressed via &lt;NoWarn&gt; in the .csproj —
/// [DependencyProperty] backing fields are read by the source generator.
/// </remarks>
public partial class CameraGrid
{
    /// <summary>
    /// Tiles that have been instantiated and are alive in memory. Maintained
    /// independently of the visual tree because RDP disconnect causes WPF to
    /// orphan the tiles (Unloaded fires, but Loaded never fires back), and the
    /// ItemContainerGenerator-based lookup in <see cref="GetCameraTileAt"/>
    /// returns null in that state. Recovery paths (e.g. session-switch driven
    /// player recreation) need a registry that survives transient unloads.
    /// </summary>
    private readonly HashSet<CameraTile> trackedTiles = [];

    [DependencyProperty(DefaultValue = 1)]
    private int gridRowCount;

    // Zero lets the columns follow from the rows and the number of cameras.
    [DependencyProperty(DefaultValue = 0)]
    private int gridColumnCount;

    // The grid's size. Automatic grows with the cameras and shows them all;
    // a fixed size shows one page of them. A camera that is not on the page
    // stays connected (and recorded): it is only not shown.
    [DependencyProperty(DefaultValue = GridPreset.Auto, PropertyChangedCallback = nameof(OnGridPagingChanged))]
    private GridPreset gridPreset;

    [DependencyProperty(DefaultValue = 0, PropertyChangedCallback = nameof(OnGridPagingChanged))]
    private int currentPage;

    [DependencyProperty(DefaultValue = 1)]
    private int pageCount;

    // "2 / 3"; empty when there is only one page.
    [DependencyProperty(DefaultValue = "")]
    private string pageLabel = string.Empty;

    private bool isApplyingPage;

    [DependencyProperty(PropertyChangedCallback = nameof(OnCameraTilesChanged))]
    private ObservableCollection<CameraConfiguration> cameraTiles = [];

    [DependencyProperty(DefaultValue = true)]
    private bool autoSave = true;

    [DependencyProperty(DefaultValue = true)]
    private bool showOverlayTitle;

    [DependencyProperty(DefaultValue = true)]
    private bool showOverlayDescription;

    [DependencyProperty(DefaultValue = true)]
    private bool showOverlayConnectionStatus;

    [DependencyProperty(DefaultValue = true)]
    private bool showOverlayQuickActions;

    [DependencyProperty(DefaultValue = false)]
    private bool showOverlayConnectionTypeIcon;

    [DependencyProperty(DefaultValue = ConnectionTypeIconPlacement.SameRow)]
    private ConnectionTypeIconPlacement connectionTypeIconPlacement;

    [DependencyProperty(DefaultValue = false)]
    private bool showOverlayTime;

    [DependencyProperty(DefaultValue = 0.6)]
    private double overlayOpacity;

    [DependencyProperty(DefaultValue = true)]
    private bool allowDragAndDropReorder;

    [DependencyProperty]
    private string? snapshotPath;

    [DependencyProperty(DefaultValue = true)]
    private bool autoConnectOnLoad;

    // Connection settings
    [DependencyProperty(DefaultValue = 10)]
    private int connectionTimeoutSeconds;

    [DependencyProperty(DefaultValue = 10)]
    private int reconnectDelaySeconds;

    [DependencyProperty(DefaultValue = true)]
    private bool autoReconnectOnFailure;

    // Notification settings
    [DependencyProperty(DefaultValue = true)]
    private bool showNotificationOnDisconnect;

    [DependencyProperty(DefaultValue = false)]
    private bool showNotificationOnReconnect;

    [DependencyProperty(DefaultValue = false)]
    private bool playNotificationSound;

    // Performance settings
    [DependencyProperty(DefaultValue = "Auto")]
    private string videoQuality = "Auto";

    [DependencyProperty(DefaultValue = true)]
    private bool hardwareAcceleration;

    // Recording/Motion Detection/Timelapse/Notification services
    [DependencyProperty(PropertyChangedCallback = nameof(OnRecordingServiceChanged))]
    private IRecordingService? recordingService;

    [DependencyProperty(PropertyChangedCallback = nameof(OnMotionDetectionServiceChanged))]
    private IMotionDetectionService? motionDetectionService;

    [DependencyProperty(PropertyChangedCallback = nameof(OnTimelapseServiceChanged))]
    private ITimelapseService? timelapseService;

    [DependencyProperty(PropertyChangedCallback = nameof(OnToastNotificationServiceChanged))]
    private IToastNotificationService? toastNotificationService;

    [DependencyProperty(PropertyChangedCallback = nameof(OnVideoPlayerFactoryChanged))]
    private IVideoPlayerFactory? videoPlayerFactory;

    [DependencyProperty(PropertyChangedCallback = nameof(OnMediaPipelineFactoryChanged))]
    private Func<IVideoPlayer, IMediaPipeline>? mediaPipelineFactory;

    [DependencyProperty(PropertyChangedCallback = nameof(OnTileLoggerChanged))]
    private ILogger? tileLogger;

    // Recording settings
    [DependencyProperty(DefaultValue = false)]
    private bool enableRecordingOnMotion;

    [DependencyProperty(DefaultValue = false)]
    private bool enableRecordingOnConnect;

    [DependencyProperty(DefaultValue = VideoTranscodeCodec.None)]
    private VideoTranscodeCodec transcodeVideoCodec;

    // Motion detection settings
    [DependencyProperty(DefaultValue = 30)]
    private int motionSensitivity;

    [DependencyProperty(DefaultValue = 2.0)]
    private double motionMinimumChangePercent;

    [DependencyProperty(DefaultValue = MotionDetectorOptions.DefaultSmallestObjectPercent)]
    private double motionSmallestObjectPercent;

    [DependencyProperty(DefaultValue = MotionDetectorOptions.DefaultStoppedObjectSeconds)]
    private int motionStoppedObjectSeconds;

    [DependencyProperty(DefaultValue = true)]
    private bool motionIgnoreLightingChanges;

    // The longer side of the analysed picture.
    [DependencyProperty(DefaultValue = MotionAnalysisSize.DefaultLongerSide)]
    private int motionAnalysisWidth;

    [DependencyProperty(DefaultValue = 2)]
    private int motionAnalysisFrameRate;

    [DependencyProperty(DefaultValue = 10)]
    private int motionPostDurationSeconds;

    [DependencyProperty(DefaultValue = 5)]
    private int motionCooldownSeconds;

    [DependencyProperty(DefaultValue = 100)]
    private int motionBoundingBoxMinArea;

    [DependencyProperty(DefaultValue = 4)]
    private int motionBoundingBoxPadding;

    // Bounding box display settings
    [DependencyProperty(DefaultValue = false)]
    private bool showBoundingBoxInGrid;

    [DependencyProperty(DefaultValue = "Red")]
    private string boundingBoxColor = "Red";

    [DependencyProperty(DefaultValue = 2)]
    private int boundingBoxThickness;

    [DependencyProperty(DefaultValue = 0.3)]
    private double boundingBoxSmoothing;

    // An app that uses this control from the package may never touch Translations, and
    // Atc.Wpf finds the texts only in assemblies already loaded: load them before the XAML.
    static CameraGrid()
    {
        _ = Translations.ResourceManager;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CameraGrid"/> class.
    /// </summary>
    public CameraGrid()
    {
        InitializeComponent();

        CameraItemsControl.ItemContainerGenerator.StatusChanged += OnTileContainersStatusChanged;
        CameraTiles = [];

        RegisterMessages();
    }

    /// <summary>
    /// Registers a tile so it can be reached without a visual-tree walk.
    /// Called by <see cref="CameraTile"/> on its first Loaded event. Idempotent.
    /// </summary>
    internal void RegisterTile(CameraTile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        // RDP-driven WPF teardowns can leave the original tile alive in memory
        // (deferred Dispose) while WPF instantiates a brand-new tile to fill the
        // ItemsControl container. Both end up registered with the same Camera.Id.
        // The new tile is the one in the visual tree (what the user sees); the
        // old one is a zombie holding a now-invisible player. Dispose the zombie
        // so its player and recording shut down cleanly, then inject services
        // into the new tile so it creates its own fresh player.
        var cameraId = tile.Camera?.Id;
        if (cameraId is not null)
        {
            var zombie = trackedTiles.FirstOrDefault(existing =>
                !ReferenceEquals(existing, tile) &&
                existing.Camera?.Id == cameraId);

            if (zombie is not null)
            {
                trackedTiles.Remove(zombie);

                var logger = TileLogger;
                if (logger is not null)
                {
                    LogZombieReplaced(logger, zombie.Camera?.Display.DisplayName ?? "<no-camera>");
                }

                zombie.MarkForRemoval();
                zombie.Dispose();
            }
        }

        trackedTiles.Add(tile);

        // Inject services into the new tile so its deferred player init runs
        // (Camera DP was set before the factory was available, so Player is
        // null until services arrive). Defer one dispatcher tick to let the
        // visual tree settle around the new tile.
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() => InitializeServicesForTile(tile)));
    }

    /// <summary>
    /// Unregisters a tile when it's about to be permanently disposed.
    /// </summary>
    internal void UnregisterTile(CameraTile tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        trackedTiles.Remove(tile);
    }

    private void InitializeServicesForTile(CameraTile tile)
    {
        if (!trackedTiles.Contains(tile))
        {
            // Tile was disposed before this deferred call ran.
            return;
        }

        tile.InitializeServices(
            RecordingService,
            MotionDetectionService,
            TimelapseService,
            ToastNotificationService,
            VideoPlayerFactory,
            MediaPipelineFactory,
            TileLogger);
    }

    /// <summary>
    /// Occurs when a full screen request is made for a camera.
    /// </summary>
    public event EventHandler<FullScreenRequestedEventArgs>? FullScreenRequested;

    /// <summary>
    /// Occurs when a camera's connection state changes.
    /// </summary>
    public event EventHandler<CameraConnectionChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Occurs when camera positions change (for auto-save).
    /// </summary>
    public event EventHandler<CameraPositionChangedEventArgs>? PositionChanged;

    /// <summary>
    /// Occurs when an edit camera request is made.
    /// </summary>
    public event EventHandler<CameraConfiguration>? EditCameraRequested;

    /// <summary>
    /// Occurs when a delete camera request is made.
    /// </summary>
    public event EventHandler<CameraConfiguration>? DeleteCameraRequested;

    /// <summary>
    /// Occurs when a tile-level command (e.g. Rotate) mutates a camera config
    /// and the host should persist the updated value.
    /// </summary>
    public event EventHandler<CameraConfiguration>? CameraConfigurationChanged;

    /// <summary>
    /// Adds a camera to the wall.
    /// </summary>
    /// <param name="camera">The camera configuration to add.</param>
    public void AddCamera(CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        CameraTiles.Add(camera);
        UpdateGridLayout();
        UpdateEmptyState();
    }

    /// <summary>
    /// Removes a camera from the wall.
    /// </summary>
    /// <param name="cameraId">The identifier of the camera to remove.</param>
    /// <returns>True if the camera was removed.</returns>
    public bool RemoveCamera(Guid cameraId)
    {
        var camera = CameraTiles.FirstOrDefault(c => c.Id == cameraId);
        if (camera is null)
        {
            return false;
        }

        var index = CameraTiles.IndexOf(camera);
        GetCameraTileAt(index)?.MarkForRemoval();

        CameraTiles.Remove(camera);
        UpdateGridLayout();
        UpdateEmptyState();
        return true;
    }

    /// <summary>
    /// Swaps a camera with an adjacent camera.
    /// This optimized implementation swaps the Player instances between CameraTile controls
    /// to avoid disconnecting and reconnecting streams.
    /// </summary>
    /// <param name="cameraId">The identifier of the camera to swap.</param>
    /// <param name="direction">The direction to swap.</param>
    public void SwapCamera(
        Guid cameraId,
        SwapDirection direction)
    {
        if (CameraTiles is null)
        {
            return;
        }

        var sourceIndex = CameraTiles
            .ToList()
            .FindIndex(c => c.Id == cameraId);

        if (sourceIndex < 0)
        {
            return;
        }

        var targetIndex = direction == SwapDirection.Left ? sourceIndex - 1 : sourceIndex + 1;

        if (targetIndex < 0 || targetIndex >= CameraTiles.Count)
        {
            return;
        }

        var sourceCamera = CameraTiles[sourceIndex];
        PerformSwap(sourceIndex, targetIndex, sourceCamera);
    }

    /// <summary>
    /// Recreates players for all connected cameras with current settings.
    /// Call this after performance settings change to apply new VideoQuality/HardwareAcceleration.
    /// </summary>
    public void RecreateConnectedPlayers()
    {
        // Use the registry, not the visual tree — see trackedTiles docs.
        // Snapshot first so RecreatePlayer mutations don't invalidate enumeration.
        var snapshot = trackedTiles.ToArray();
        var logger = TileLogger;

        if (logger is not null)
        {
            LogRecreateTrackedCount(logger, snapshot.Length);
        }

        foreach (var tile in snapshot)
        {
            var name = tile.Camera?.Display.DisplayName ?? "<no-camera>";
            var state = tile.ConnectionState;

            if (state != ConnectionState.Connected)
            {
                if (logger is not null)
                {
                    LogRecreateSkipped(logger, name, state);
                }

                continue;
            }

            if (logger is not null)
            {
                LogRecreateRecreating(logger, name);
            }

            tile.RecreatePlayer();
        }
    }

    /// <summary>
    /// Sets the device-unplugged flag on the tile bound to the
    /// specified camera id. Called by the camera-wall manager (or the
    /// API-client equivalent) from the
    /// <see cref="AtcSoft.VideoSurveillance.Services.IUsbCameraWatcher"/>
    /// events — on
    /// <see cref="AtcSoft.VideoSurveillance.Services.IUsbCameraWatcher.DeviceRemoved"/>
    /// with <paramref name="unplugged"/> = <c>true</c>, on
    /// <see cref="AtcSoft.VideoSurveillance.Services.IUsbCameraWatcher.DeviceArrived"/>
    /// with <c>false</c>. The lookup is via the tracked-tile registry
    /// (not the visual tree) so unplug events received before the tile
    /// container materializes still hit the right instance once it
    /// shows up.
    /// </summary>
    public void SetUsbDeviceUnplugged(
        Guid cameraId,
        bool unplugged)
    {
        var tile = trackedTiles.FirstOrDefault(t => t.Camera?.Id == cameraId);
        if (tile is null)
        {
            return;
        }

        tile.IsDeviceUnplugged = unplugged;
    }

    /// <summary>
    /// Forces the tile bound to the specified camera id to re-read its
    /// display-time settings (rotation, overlay position, name, description)
    /// after the Edit Camera dialog has mutated the configuration in place.
    /// </summary>
    public void RefreshCameraDisplay(Guid cameraId)
    {
        if (CameraTiles is null)
        {
            return;
        }

        var index = CameraTiles.ToList().FindIndex(c => c.Id == cameraId);
        if (index < 0)
        {
            return;
        }

        GetCameraTileAt(index)?.RefreshFromCameraSettings();
    }

    /// <summary>
    /// Takes the picture a camera's tile shows now, turned by the camera's rotation as it is stored,
    /// without opening the camera again (a USB camera opens only once).
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The picture as PNG, or <see langword="null"/> when no tile shows the camera.</returns>
    public Task<byte[]?> CapturePictureAsync(
        Guid cameraId,
        CancellationToken cancellationToken = default)
    {
        var index = CameraTiles?.ToList().FindIndex(c => c.Id == cameraId) ?? -1;
        return index >= 0 && GetCameraTileAt(index) is { } tile
            ? tile.CapturePictureAsync(cancellationToken)
            : Task.FromResult<byte[]?>(null);
    }

    /// <summary>
    /// Gets the CameraTile control at the specified collection index.
    /// </summary>
    /// <param name="index">The index in the CameraTiles collection.</param>
    /// <returns>The CameraTile control, or null if not found.</returns>
    private CameraTile? GetCameraTileAt(int index)
    {
        var container = CameraItemsControl.ItemContainerGenerator.ContainerFromIndex(index);
        if (container is null)
        {
            return null;
        }

        return VisualTreeHelperEx.FindChild<CameraTile>(container);
    }

    /// <summary>
    /// Performs an optimized swap between two camera positions.
    /// Keeps players in place and swaps streams to avoid disconnecting/reconnecting.
    /// </summary>
    private void PerformSwap(
        int sourceIndex,
        int targetIndex,
        CameraConfiguration sourceCamera)
    {
        var sourceTile = GetCameraTileAt(sourceIndex);
        var targetTile = GetCameraTileAt(targetIndex);

        if (sourceTile is not null && targetTile is not null)
        {
            var targetCamera = CameraTiles[targetIndex];

            sourceTile.PrepareForSwap();
            targetTile.PrepareForSwap();

            CameraTiles[sourceIndex] = targetCamera;
            CameraTiles[targetIndex] = sourceCamera;

            if (sourceTile.ConnectionState == ConnectionState.Connected &&
                targetTile.ConnectionState == ConnectionState.Connected)
            {
                // Both connected — swap players to keep streams alive
                sourceTile.SwapPlayerWith(targetTile);
            }
            else
            {
                // At least one not connected — close then re-open streams with new camera configs
                sourceTile.Reconnect();
                targetTile.Reconnect();
            }

            sourceTile.CompleteSwap();
            targetTile.CompleteSwap();
        }
        else
        {
            // Reorder fallback: ItemsControl recycles containers, so the source
            // tile is torn down and a new one is generated at the target index.
            // Mark the outgoing tile so OnUnloaded actually disposes its player.
            GetCameraTileAt(sourceIndex)?.MarkForRemoval();
            CameraTiles.RemoveAt(sourceIndex);
            CameraTiles.Insert(targetIndex, sourceCamera);
        }

        UpdateSwapCapabilities();
        PositionChanged?.Invoke(this, new CameraPositionChangedEventArgs(sourceCamera, sourceIndex, targetIndex));
    }

    /// <summary>
    /// Clears all cameras from the wall.
    /// </summary>
    public void Clear()
    {
        for (var i = 0; i < CameraTiles.Count; i++)
        {
            GetCameraTileAt(i)?.MarkForRemoval();
        }

        CameraTiles.Clear();
        UpdateGridLayout();
        UpdateEmptyState();
    }

    /// <summary>
    /// Reconnects all camera streams.
    /// </summary>
    public void ReconnectAll()
    {
        for (var i = 0; i < CameraTiles.Count; i++)
        {
            var tile = GetCameraTileAt(i);
            tile?.Reconnect();
        }
    }

    /// <summary>
    /// Gets the current layout as a list of camera layout items.
    /// </summary>
    /// <returns>The list of camera layout items in order.</returns>
    [SuppressMessage("Design", "MA0016:Prefer using collection abstraction instead of implementation", Justification = "Return type matches CameraLayout.Items property type")]
    public List<CameraLayoutItem> GetCurrentLayout()
        => CameraTiles
            .Select(
                (camera, index) => new CameraLayoutItem
                {
                    CameraId = camera.Id,
                    OrderNumber = index,
                })
            .ToList();

    /// <summary>
    /// Applies a layout to the camera wall.
    /// Reuses cameras that exist in both the current and new layout to avoid
    /// unnecessary stream disconnection/reconnection. Shared cameras keep their
    /// tiles and active connections — reordering is done via swap operations
    /// (same mechanism as <see cref="PerformSwap"/>) that exchange players between
    /// tiles without disposing them. Only cameras unique to the old or new layout
    /// are removed or inserted.
    /// </summary>
    /// <param name="layout">The layout to apply.</param>
    /// <param name="cameras">The available cameras.</param>
    public void ApplyLayout(
        CameraLayout layout,
        IEnumerable<CameraConfiguration> cameras)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(cameras);

        var cameraDict = cameras.ToDictionary(c => c.Id);

        // Build the target list of cameras in layout order
        var targetCameras = layout.Items
            .Where(item => cameraDict.ContainsKey(item.CameraId))
            .OrderBy(i => i.OrderNumber)
            .Select(item => cameraDict[item.CameraId])
            .ToList();

        var targetIds = new HashSet<Guid>(targetCameras.Select(c => c.Id));
        var currentIds = new HashSet<Guid>(CameraTiles.Select(c => c.Id));
        var sharedIds = new HashSet<Guid>(currentIds.Where(targetIds.Contains));

        // Remove cameras not in the target layout (iterating backwards to preserve indices)
        for (var i = CameraTiles.Count - 1; i >= 0; i--)
        {
            if (!targetIds.Contains(CameraTiles[i].Id))
            {
                GetCameraTileAt(i)?.MarkForRemoval();
                CameraTiles.RemoveAt(i);
            }
        }

        // Reorder shared cameras using swap operations (selection sort) to preserve
        // player connections. Uses the same PrepareForSwap/SwapPlayerWith/CompleteSwap
        // mechanism as PerformSwap — collection items are replaced via index assignment
        // which keeps containers stable (no dispose/recreate).
        var sharedTargetOrder = targetCameras
            .Where(c => sharedIds.Contains(c.Id))
            .ToList();

        for (var i = 0; i < sharedTargetOrder.Count; i++)
        {
            if (CameraTiles[i].Id == sharedTargetOrder[i].Id)
            {
                continue;
            }

            var swapIndex = -1;
            for (var j = i + 1; j < CameraTiles.Count; j++)
            {
                if (CameraTiles[j].Id == sharedTargetOrder[i].Id)
                {
                    swapIndex = j;
                    break;
                }
            }

            if (swapIndex < 0)
            {
                continue;
            }

            var tileA = GetCameraTileAt(i);
            var tileB = GetCameraTileAt(swapIndex);

            if (tileA is not null && tileB is not null)
            {
                var cameraA = CameraTiles[i];
                var cameraB = CameraTiles[swapIndex];

                tileA.PrepareForSwap();
                tileB.PrepareForSwap();

                CameraTiles[i] = cameraB;
                CameraTiles[swapIndex] = cameraA;

                if (tileA.ConnectionState == ConnectionState.Connected &&
                    tileB.ConnectionState == ConnectionState.Connected)
                {
                    tileA.SwapPlayerWith(tileB);
                }
                else
                {
                    tileA.Reconnect();
                    tileB.Reconnect();
                }

                tileA.CompleteSwap();
                tileB.CompleteSwap();
            }
        }

        // Insert new cameras at their target positions
        for (var i = 0; i < targetCameras.Count; i++)
        {
            if (i < CameraTiles.Count && CameraTiles[i].Id == targetCameras[i].Id)
            {
                continue;
            }

            if (!sharedIds.Contains(targetCameras[i].Id))
            {
                CameraTiles.Insert(i, targetCameras[i]);
            }
        }

        UpdateGridLayout();
        UpdateEmptyState();
    }

    private static void OnCameraTilesChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraGrid grid)
        {
            grid.UpdateGridLayout();
            grid.UpdateEmptyState();
            grid.UpdateSwapCapabilities();

            if (e.OldValue is ObservableCollection<CameraConfiguration> oldCollection)
            {
                oldCollection.CollectionChanged -= grid.OnCameraTilesCollectionChanged;
            }

            if (e.NewValue is ObservableCollection<CameraConfiguration> newCollection)
            {
                newCollection.CollectionChanged += grid.OnCameraTilesCollectionChanged;
            }
        }
    }

    private void OnCameraTilesCollectionChanged(
        object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        UpdateGridLayout();
        UpdateEmptyState();
        UpdateSwapCapabilities();

        // Initialize services for newly added tiles
        // This is done on a deferred dispatcher call to ensure the visual tree is built
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add ||
            e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            _ = Dispatcher.BeginInvoke(
                            DispatcherPriority.Loaded,
                            new Action(InitializeAllTileServices));
        }
    }

    private void RegisterMessages()
    {
        Messenger.Default.Register<CameraAddMessage>(this, OnCameraAddMessage);
        Messenger.Default.Register<CameraRemoveMessage>(this, OnCameraRemoveMessage);
        Messenger.Default.Register<CameraSwapMessage>(this, OnCameraSwapMessage);
    }

    private void OnCameraAddMessage(CameraAddMessage message)
    {
        Dispatcher.Invoke(() => AddCamera(message.Camera));
    }

    private void OnCameraRemoveMessage(CameraRemoveMessage message)
    {
        Dispatcher.Invoke(() => RemoveCamera(message.CameraId));
    }

    private void OnCameraSwapMessage(CameraSwapMessage message)
    {
        Dispatcher.Invoke(() => SwapCamera(message.CameraId, message.Direction));
    }

    /// <summary>
    /// Occurs when the page shown, or the number of pages, has changed.
    /// </summary>
    public event EventHandler? PagingChanged;

    /// <summary>
    /// Shows the next page of cameras; after the last comes the first.
    /// </summary>
    public void ShowNextPage()
        => CurrentPage = PageCount <= 1 ? 0 : (CurrentPage + 1) % PageCount;

    /// <summary>
    /// Shows the previous page of cameras; before the first comes the last.
    /// </summary>
    public void ShowPreviousPage()
        => CurrentPage = PageCount <= 1 ? 0 : (CurrentPage + PageCount - 1) % PageCount;

    private static void OnGridPagingChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraGrid grid && !grid.isApplyingPage)
        {
            grid.UpdateGridLayout();
        }
    }

    private void UpdateGridLayout()
    {
        var page = GridPager.Calculate(CameraTiles?.Count ?? 0, GridPreset, CurrentPage);

        isApplyingPage = true;
        try
        {
            GridRowCount = page.Rows;
            GridColumnCount = page.Columns;
            PageCount = page.PageCount;
            CurrentPage = page.PageIndex;
            PageLabel = page.Label;
        }
        finally
        {
            isApplyingPage = false;
        }

        ApplyPage(page);
        PagingChanged?.Invoke(this, EventArgs.Empty);
    }

    // Hides the tiles that are not on the page. A hidden tile is left out
    // of the grid's layout but keeps its camera open.
    private void ApplyPage(GridPage page)
    {
        var generator = CameraItemsControl.ItemContainerGenerator;
        for (var i = 0; i < CameraItemsControl.Items.Count; i++)
        {
            if (generator.ContainerFromIndex(i) is UIElement container)
            {
                container.Visibility = page.Shows(i) ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    // The tiles of cameras added just now do not exist yet when the
    // collection changes; the page is applied again once they do.
    private void OnTileContainersStatusChanged(
        object? sender,
        EventArgs e)
    {
        if (CameraItemsControl.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
        {
            ApplyPage(GridPager.Calculate(CameraTiles?.Count ?? 0, GridPreset, CurrentPage));
        }
    }

    private void UpdateEmptyState()
    {
        EmptyStateText.Visibility = CameraTiles.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateSwapCapabilities()
    {
        if (CameraTiles is null)
        {
            return;
        }

        for (var i = 0; i < CameraTiles.Count; i++)
        {
            CameraTiles[i].CanSwapLeft = i > 0;
            CameraTiles[i].CanSwapRight = i < CameraTiles.Count - 1;
        }
    }

    private void OnFullScreenRequested(
        object? sender,
        FullScreenRequestedEventArgs e)
    {
        FullScreenRequested?.Invoke(this, e);
    }

    private void OnZoomStateChanged(
        object? sender,
        EventArgs e)
    {
        // Only one tile can be zoomed at a time — reset all others
        if (sender is not CameraTile zoomedTile || !zoomedTile.IsZoomed || CameraTiles is null)
        {
            return;
        }

        for (var i = 0; i < CameraTiles.Count; i++)
        {
            var tile = GetCameraTileAt(i);
            if (tile is not null && tile != zoomedTile && tile.IsZoomed)
            {
                tile.ResetZoom();
            }
        }
    }

    private void OnSwapLeftRequested(
        object? sender,
        CameraConfiguration e)
    {
        SwapCamera(e.Id, SwapDirection.Left);
    }

    private void OnSwapRightRequested(
        object? sender,
        CameraConfiguration e)
    {
        SwapCamera(e.Id, SwapDirection.Right);
    }

    private void OnConnectionStateChanged(
        object? sender,
        CameraConnectionChangedEventArgs e)
    {
        ConnectionStateChanged?.Invoke(this, e);
    }

    private void OnEditCameraRequested(
        object? sender,
        CameraConfiguration e)
    {
        EditCameraRequested?.Invoke(this, e);
    }

    private void OnDeleteCameraRequested(
        object? sender,
        CameraConfiguration e)
    {
        DeleteCameraRequested?.Invoke(this, e);
    }

    private void OnCameraConfigurationChanged(
        object? sender,
        CameraConfiguration e)
    {
        CameraConfigurationChanged?.Invoke(this, e);
    }

    private void OnCameraDropped(
        object? sender,
        CameraConfiguration sourceCamera)
    {
        if (CameraTiles is null ||
            sender is not CameraTile targetTile ||
            targetTile.Camera is null)
        {
            return;
        }

        var sourceIndex = CameraTiles
            .ToList()
            .FindIndex(c => c.Id == sourceCamera.Id);
        var targetIndex = CameraTiles
            .ToList()
            .FindIndex(c => c.Id == targetTile.Camera.Id);

        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
        {
            return;
        }

        PerformSwap(sourceIndex, targetIndex, sourceCamera);
    }

    private static void OnRecordingServiceChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraGrid grid)
        {
            grid.InitializeAllTileServices();
        }
    }

    private static void OnMotionDetectionServiceChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraGrid grid)
        {
            grid.InitializeAllTileServices();
        }
    }

    private static void OnTimelapseServiceChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraGrid grid)
        {
            grid.InitializeAllTileServices();
        }
    }

    private static void OnToastNotificationServiceChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraGrid grid)
        {
            grid.InitializeAllTileServices();
        }
    }

    private static void OnVideoPlayerFactoryChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraGrid grid)
        {
            grid.InitializeAllTileServices();
        }
    }

    private static void OnMediaPipelineFactoryChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraGrid grid)
        {
            grid.InitializeAllTileServices();
        }
    }

    private static void OnTileLoggerChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraGrid grid)
        {
            grid.InitializeAllTileServices();
        }
    }

    /// <summary>
    /// Initializes all camera tiles with the recording, motion detection, and timelapse services.
    /// </summary>
    private void InitializeAllTileServices()
    {
        if (CameraTiles is null)
        {
            return;
        }

        for (var i = 0; i < CameraTiles.Count; i++)
        {
            var tile = GetCameraTileAt(i);
            tile?.InitializeServices(RecordingService, MotionDetectionService, TimelapseService, ToastNotificationService, VideoPlayerFactory, MediaPipelineFactory, TileLogger);
        }
    }
}