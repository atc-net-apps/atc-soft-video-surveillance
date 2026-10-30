namespace AtcSoft.VideoPlayer.Wpf;

/// <summary>
/// WPF control that presents a <see cref="IVideoPlayer"/>'s GPU-decoded video
/// using DirectComposition. Supports XAML overlay content rendered on top of video.
/// </summary>
/// <remarks>
/// Window architecture (same pattern as FlyleafHost):
/// <code>
/// WPF Owner Window
///   └── VideoHost (ContentControl in visual tree)
///         ├── Surface Window (WS_CHILD, WS_EX_NOREDIRECTIONBITMAP)
///         │     └── DComp Target → Visual → SwapChain
///         └── Overlay Window (WS_CHILD of Surface, transparent)
///               └── VideoHost.Content (XAML overlay elements)
/// </code>
/// </remarks>
public partial class VideoHost : ContentControl, IDisposable
{
    private const int GwlStyle = -16;
    private const int GwlExstyle = -20;
    private const int WsChild = 0x40000000;
    private const int WsClipSiblings = 0x04000000;
    private const int WsClipChildren = 0x02000000;
    private const int WsVisible = 0x10000000;
    private const int WsExNoRedirectionBitmap = 0x00200000;

    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private const int SwHide = 0;
    private const int SwShow = 5;

    /// <summary>
    /// Identifies the <see cref="Player"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PlayerProperty =
        DependencyProperty.Register(
            nameof(Player),
            typeof(IVideoPlayer),
            typeof(VideoHost),
            new PropertyMetadata(
                null,
                OnPlayerChanged));

    /// <summary>
    /// Identifies the <see cref="BackColor"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty BackColorProperty =
        DependencyProperty.Register(
            nameof(BackColor),
            typeof(Color),
            typeof(VideoHost),
            new PropertyMetadata(
                Colors.Black,
                OnBackColorChanged));

    private readonly OverlayBridge bridge = new();

    private Window? ownerWindow;
    private Window? surfaceWindow;
    private Window? overlayWindow;
    private nint surfaceHwnd;
    private nint ownerHwnd;

    private SwapChainPresenter? presenter;
    private D3D11Accelerator? boundAccelerator;
    private int lastX;
    private int lastY;
    private int lastW;
    private int lastH;
    private bool disposed;

    public VideoHost()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
        DataContextChanged += OnDataContextChanged;
    }

    /// <inheritdoc />
    protected override void OnContentChanged(
        object oldContent,
        object newContent)
    {
        base.OnContentChanged(oldContent, newContent);

        // Pre-set the OverlayBridge DataContext on content immediately so that
        // {Binding HostDataContext.xxx} bindings resolve without errors before
        // the overlay window is created and content is moved there.
        if (overlayWindow is null && newContent is FrameworkElement fe)
        {
            bridge.HostDataContext = DataContext;
            fe.DataContext = bridge;
        }
    }

    /// <summary>
    /// Gets or sets the video player to display.
    /// </summary>
    public IVideoPlayer? Player
    {
        get => (IVideoPlayer?)GetValue(PlayerProperty);
        set => SetValue(PlayerProperty, value);
    }

    /// <summary>
    /// Gets the overlay window, or <c>null</c> if not yet created.
    /// </summary>
    public Window? Overlay => overlayWindow;

    /// <summary>
    /// Gets the surface window, or <c>null</c> if not yet created.
    /// </summary>
    public Window? Surface => surfaceWindow;

    /// <summary>
    /// Occurs after the overlay window has been created.
    /// </summary>
    public event EventHandler? OverlayCreated;

    /// <summary>
    /// Occurs after the surface window has been created.
    /// </summary>
    public event EventHandler? SurfaceCreated;

    /// <summary>
    /// Gets or sets the background color when no video is playing.
    /// </summary>
    public Color BackColor
    {
        get => (Color)GetValue(BackColorProperty);
        set => SetValue(BackColorProperty, value);
    }

    /// <summary>
    /// Sets the video viewport zoom and pan.
    /// </summary>
    /// <param name="zoom">Zoom level (1.0 = fit to control, >1.0 = zoomed in).</param>
    /// <param name="panX">Horizontal pan in normalized coordinates (-1.0 to 1.0).</param>
    /// <param name="panY">Vertical pan in normalized coordinates (-1.0 to 1.0).</param>
    public void SetZoom(
        float zoom,
        float panX = 0f,
        float panY = 0f)
    {
        presenter?.SetZoom(zoom, panX, panY);
    }

    /// <summary>
    /// Resets the video viewport to fit-to-control (no zoom, no pan).
    /// </summary>
    public void ResetZoom()
    {
        presenter?.SetZoom(1.0f, 0f, 0f);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        if (disposing)
        {
            DetachPlayer();

            overlayWindow?.Close();
            overlayWindow = null;

            surfaceWindow?.Close();
            surfaceWindow = null;
        }
    }

    private static void OnPlayerChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is VideoHost host)
        {
            host.DetachPlayer();

            if (e.NewValue is IVideoPlayer newPlayer)
            {
                host.AttachPlayer(newPlayer);
            }
        }
    }

    private static void OnBackColorChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is VideoHost host && host.surfaceWindow is not null)
        {
            var color = (Color)e.NewValue;
            host.surfaceWindow.Background = new SolidColorBrush(color);
        }
    }

    private void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        ownerWindow = Window.GetWindow(this);
        if (ownerWindow is null)
        {
            return;
        }

        ownerHwnd = new WindowInteropHelper(ownerWindow).Handle;
        if (ownerHwnd == nint.Zero)
        {
            return;
        }

        CreateSurfaceWindow();
        SurfaceCreated?.Invoke(this, EventArgs.Empty);

        CreateOverlayWindow();
        OverlayCreated?.Invoke(this, EventArgs.Empty);

        UpdateWindowPositions();

        ownerWindow.LocationChanged += OnOwnerLocationChanged;
        ownerWindow.SizeChanged += OnOwnerSizeChanged;
        LayoutUpdated += OnLayoutUpdated;

        if (Player is not null)
        {
            AttachPlayer(Player);
        }
    }

    private void OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        if (ownerWindow is not null)
        {
            ownerWindow.LocationChanged -= OnOwnerLocationChanged;
            ownerWindow.SizeChanged -= OnOwnerSizeChanged;
        }

        LayoutUpdated -= OnLayoutUpdated;

        DetachPlayer();

        overlayWindow?.Close();
        overlayWindow = null;

        surfaceWindow?.Close();
        surfaceWindow = null;

        ownerWindow = null;
        ownerHwnd = nint.Zero;
        surfaceHwnd = nint.Zero;
    }

    private void OnIsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (surfaceHwnd == nint.Zero)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            ShowWindow(surfaceHwnd, SwShow);
            UpdateWindowPositions();
        }
        else
        {
            ShowWindow(surfaceHwnd, SwHide);
        }
    }

    private void CreateSurfaceWindow()
    {
        surfaceWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Background = new SolidColorBrush(BackColor),
        };

        var helper = new WindowInteropHelper(surfaceWindow) { Owner = ownerHwnd };
        surfaceHwnd = helper.EnsureHandle();

        // Set WS_CHILD and clear title bar bits.
        var style = GetWindowLong(surfaceHwnd, GwlStyle);
        style = (style | WsChild | WsClipSiblings | WsClipChildren | WsVisible) & ~0x00CF0000;
        _ = SetWindowLong(surfaceHwnd, GwlStyle, style);

        // Set WS_EX_NOREDIRECTIONBITMAP so DComp composites directly.
        var exStyle = GetWindowLong(surfaceHwnd, GwlExstyle);
        exStyle |= WsExNoRedirectionBitmap;
        _ = SetWindowLong(surfaceHwnd, GwlExstyle, exStyle);

        SetParent(surfaceHwnd, ownerHwnd);
        surfaceWindow.Show();
    }

    private void CreateOverlayWindow()
    {
        overlayWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
        };

        var helper = new WindowInteropHelper(overlayWindow) { Owner = ownerHwnd };
        var overlayHwnd = helper.EnsureHandle();

        var style = GetWindowLong(overlayHwnd, GwlStyle);
        style = (style | WsChild | WsClipSiblings | WsVisible) & ~0x00CF0000;
        _ = SetWindowLong(overlayHwnd, GwlStyle, style);

        SetParent(overlayHwnd, surfaceHwnd);
        overlayWindow.Show();

        // Move VideoHost's Content to the overlay window.
        var content = Content;
        Content = null;
        overlayWindow.Content = content;

        // Reuse the bridge created in OnContentChanged so existing bindings
        // continue working without re-evaluation.
        bridge.HostDataContext = DataContext;
        overlayWindow.DataContext = bridge;
    }

    private void OnDataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        bridge.HostDataContext = e.NewValue;
    }

    private void UpdateWindowPositions()
    {
        if (surfaceWindow is null || ownerWindow is null)
        {
            return;
        }

        // Don't reposition (with SWP_SHOWWINDOW) while native windows are hidden.
        if (!IsVisible)
        {
            return;
        }

        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
        {
            return;
        }

        // Transform VideoHost's position to the owner window's coordinate space.
        var transform = TransformToAncestor(ownerWindow);
        var topLeft = transform.Transform(new Point(0, 0));
        var bottomRight = transform.Transform(new Point(ActualWidth, ActualHeight));

        // Account for DPI scaling.
        var dpiTransform = source.CompositionTarget.TransformToDevice;
        var topLeftDevice = dpiTransform.Transform(topLeft);
        var bottomRightDevice = dpiTransform.Transform(bottomRight);

        int x = (int)topLeftDevice.X;
        int y = (int)topLeftDevice.Y;
        int w = (int)(bottomRightDevice.X - topLeftDevice.X);
        int h = (int)(bottomRightDevice.Y - topLeftDevice.Y);

        if (w <= 0 || h <= 0)
        {
            return;
        }

        // Skip if position and size haven't changed (LayoutUpdated fires very
        // frequently — redundant SetWindowPos/Resize calls cause black flicker).
        if (x == lastX && y == lastY && w == lastW && h == lastH)
        {
            return;
        }

        lastX = x;
        lastY = y;
        lastW = w;
        lastH = h;

        SetWindowPos(
            surfaceHwnd,
            nint.Zero,
            x,
            y,
            w,
            h,
            SwpNoActivate | SwpShowWindow);

        if (overlayWindow is not null)
        {
            var overlayHwnd = new WindowInteropHelper(overlayWindow).Handle;
            SetWindowPos(
                overlayHwnd,
                nint.Zero,
                0,
                0,
                w,
                h,
                SwpNoActivate | SwpShowWindow);
        }

        presenter?.Resize(w, h);
    }

    private void AttachPlayer(IVideoPlayer player)
    {
        if (surfaceHwnd == nint.Zero)
        {
            return;
        }

        if (player.GpuAccelerator is D3D11Accelerator accelerator)
        {
            presenter = new SwapChainPresenter(
                accelerator.D3D11DeviceRef,
                surfaceHwnd);
            boundAccelerator = accelerator;
            accelerator.FrameReady += OnFrameReady;

            // Apply initial scale if the control already has a size.
            if (ActualWidth > 0 && ActualHeight > 0)
            {
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget is not null)
                {
                    var dpi = source.CompositionTarget.TransformToDevice;
                    int w = (int)(ActualWidth * dpi.M11);
                    int h = (int)(ActualHeight * dpi.M22);
                    presenter.Resize(w, h);
                }
            }
        }
    }

    private void DetachPlayer()
    {
        if (boundAccelerator is not null)
        {
            boundAccelerator.FrameReady -= OnFrameReady;
            boundAccelerator = null;
        }

        presenter?.Dispose();
        presenter = null;
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Texture is owned by the renderer; this method only forwards a non-owning reference to the presenter.")]
    private void OnFrameReady()
    {
        if (presenter is null || boundAccelerator is null)
        {
            return;
        }

        if (boundAccelerator.TryGetBgraTexture(
                out var texture,
                out var width,
                out var height))
        {
            presenter.Present(texture!, width, height);
        }
    }

    private void OnOwnerLocationChanged(
        object? sender,
        EventArgs e)
        => UpdateWindowPositions();

    private void OnOwnerSizeChanged(
        object sender,
        SizeChangedEventArgs e)
        => UpdateWindowPositions();

    private void OnLayoutUpdated(
        object? sender,
        EventArgs e)
        => UpdateWindowPositions();

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint SetParent(
        nint hWndChild,
        nint hWndNewParent);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int GetWindowLong(
        nint hWnd,
        int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int SetWindowLong(
        nint hWnd,
        int nIndex,
        int dwNewLong);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(
        nint hWnd,
        nint hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(
        nint hWnd,
        int nCmdShow);
}