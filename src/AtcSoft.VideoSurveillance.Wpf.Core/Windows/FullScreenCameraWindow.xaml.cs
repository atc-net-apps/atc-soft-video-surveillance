namespace AtcSoft.VideoSurveillance.Wpf.Core.Windows;

/// <summary>
/// Fullscreen window for displaying a single camera stream.
/// </summary>
public partial class FullScreenCameraWindow : IDisposable
{
    private const int WmKeyDown = 0x0100;
    private const int WmMouseWheel = 0x020A;
    private const int WmRightButtonUp = 0x0205;
    private const int VkEscape = 0x1B;
    private const int MkControl = 0x0008;

    private readonly FullScreenCameraWindowViewModel viewModel;
    private DispatcherTimer? timeUpdateTimer;
    private Point lastMousePosition;
    private MotionBoundingBoxOverlay? cachedMotionOverlay;
    private float currentZoom = 1.0f;
    private float currentPanX;
    private float currentPanY;
    private bool isPanning;
    private bool isSelecting;
    private Point panStartScreen;
    private Point selectionStartPoint;
    private float panStartX;
    private float panStartY;
    private bool disposed;

    public FullScreenCameraWindow(FullScreenCameraWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        this.viewModel = viewModel;
        DataContext = viewModel;

        viewModel.CloseRequested += OnCloseRequested;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += OnWindowClosed;

        // Use InputManager to capture mouse input before VideoHost intercepts it
        InputManager.Current.PreProcessInput += OnPreProcessInput;

        // Use ComponentDispatcher to capture keyboard at Win32 level (VideoHost uses HwndHost)
        ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;

        // Start time update timer for overlay
        StartTimeUpdateTimer();
    }

    private void StartTimeUpdateTimer()
    {
        // Update time display initially
        UpdateTimeDisplay();

        // Set overlay background with configured opacity and position
        ApplyOverlayBackground();
        ApplyOverlayPosition();

        // Create timer to update time every second
        timeUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        timeUpdateTimer.Tick += (_, _) => UpdateTimeDisplay();
        timeUpdateTimer.Start();
    }

    private void UpdateTimeDisplay()
    {
        TimeDisplay.Text = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
    }

    private void ApplyOverlayBackground()
    {
        // Apply overlay opacity to the background color
        var opacity = viewModel.OverlayOpacity;
        var color = Color.FromArgb((byte)(opacity * 255), 0, 0, 0);
        OverlayBorder.Background = new SolidColorBrush(color);
    }

    private void ApplyOverlayPosition()
    {
        var position = viewModel.OverlayPosition;

        OverlayBorder.HorizontalAlignment = position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.BottomLeft => HorizontalAlignment.Left,
            OverlayPosition.TopRight or OverlayPosition.BottomRight => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left,
        };

        OverlayBorder.VerticalAlignment = position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.TopRight => VerticalAlignment.Top,
            OverlayPosition.BottomLeft or OverlayPosition.BottomRight => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Top,
        };

        // Connection-type icon: adjacent to the info box, but the close button
        // owns the top-right corner here, so fall back to the other adjacent corner.
        var iconCorner = CameraConnectionTypeHelper.ResolveIconCorner(position, viewModel.ConnectionTypeIconPlacement);
        if (iconCorner == OverlayPosition.TopRight)
        {
            var otherPlacement = viewModel.ConnectionTypeIconPlacement == ConnectionTypeIconPlacement.SameRow
                ? ConnectionTypeIconPlacement.SameColumn
                : ConnectionTypeIconPlacement.SameRow;
            iconCorner = CameraConnectionTypeHelper.ResolveIconCorner(position, otherPlacement);
        }

        ConnectionTypeIconControl.HorizontalAlignment = iconCorner is OverlayPosition.TopLeft or OverlayPosition.BottomLeft
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Right;
        ConnectionTypeIconControl.VerticalAlignment = iconCorner is OverlayPosition.TopLeft or OverlayPosition.TopRight
            ? VerticalAlignment.Top
            : VerticalAlignment.Bottom;
    }

    private void OnViewModelPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FullScreenCameraWindowViewModel.CurrentBoundingBoxes))
        {
            UpdateMotionBoundingBoxes();
        }
    }

    private void UpdateMotionBoundingBoxes()
    {
        var motionOverlay = GetMotionBoundingBoxOverlay();
        if (motionOverlay is null)
        {
            return;
        }

        // Set analysis resolution
        motionOverlay.AnalysisWidth = viewModel.AnalysisWidth;
        motionOverlay.AnalysisHeight = viewModel.AnalysisHeight;

        // Set the video stream dimensions for letterbox-aware coordinate mapping
        // Boxes are found in the rotated picture, so letterbox with the
        // displayed size, not the stream's own (unrotated) size.
        var streamInfo = viewModel.Player?.StreamInfo;
        if (streamInfo is not null && streamInfo.Width > 0 && streamInfo.Height > 0)
        {
            var (displayWidth, displayHeight) = MotionBoxMapper.GetDisplaySize(
                streamInfo.Width,
                streamInfo.Height,
                viewModel.Rotation);
            motionOverlay.VideoWidth = displayWidth;
            motionOverlay.VideoHeight = displayHeight;
        }

        // Get the video container size for coordinate mapping
        var containerSize = new Size(VideoPlayer.ActualWidth, VideoPlayer.ActualHeight);
        if ((containerSize.Width <= 0 || containerSize.Height <= 0) &&
            VideoPlayer.Overlay is not null)
        {
            // Try to get size from the overlay window
            containerSize = new Size(VideoPlayer.Overlay.ActualWidth, VideoPlayer.Overlay.ActualHeight);
        }

        motionOverlay.UpdateMotionBoxes(viewModel.CurrentBoundingBoxes, containerSize);
    }

    private void CompleteSelectionZoom(MouseButtonEventArgs e)
    {
        isSelecting = false;
        ZoomSelectionCanvas.Visibility = Visibility.Collapsed;

        var endPoint = e.GetPosition(this);

        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var selW = Math.Abs(endPoint.X - selectionStartPoint.X);
        var selH = Math.Abs(endPoint.Y - selectionStartPoint.Y);

        // Ignore tiny selections (accidental clicks)
        if (selW < 10 || selH < 10)
        {
            return;
        }

        // Calculate zoom level from selection size
        var zoomX = (float)(ActualWidth / selW);
        var zoomY = (float)(ActualHeight / selH);
        var newZoom = Math.Clamp(Math.Min(zoomX, zoomY), 1.0f, 10.0f);

        // Calculate pan to center the selection
        var centerX = (Math.Min(selectionStartPoint.X, endPoint.X) + (selW / 2)) / ActualWidth;
        var centerY = (Math.Min(selectionStartPoint.Y, endPoint.Y) + (selH / 2)) / ActualHeight;

        currentZoom = newZoom;
        currentPanX = Math.Clamp(((float)centerX - 0.5f) * 2f, -1f, 1f);
        currentPanY = Math.Clamp(((float)centerY - 0.5f) * 2f, -1f, 1f);

        VideoPlayer.SetZoom(currentZoom, currentPanX, currentPanY);
        UpdateMotionOverlayForZoom();
    }

    private void UpdateMotionOverlayForZoom()
    {
        var overlay = GetMotionBoundingBoxOverlay();
        if (overlay is not null)
        {
            overlay.Visibility = currentZoom > 1.01f ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private MotionBoundingBoxOverlay? GetMotionBoundingBoxOverlay()
    {
        // Return cached reference if available
        if (cachedMotionOverlay is not null)
        {
            return cachedMotionOverlay;
        }

        // Try to find MotionBoundingBoxOverlay in overlay window content
        if (VideoPlayer.Overlay is null)
        {
            return null;
        }

        if (VideoPlayer.Overlay.Content is MotionBoundingBoxOverlay directOverlay)
        {
            cachedMotionOverlay = directOverlay;
        }
        else if (VideoPlayer.Overlay.Content is DependencyObject content)
        {
            // Search visual tree
            cachedMotionOverlay = content
                .FindChildren<MotionBoundingBoxOverlay>()
                .FirstOrDefault();
        }

        // If still not found, try to find in overlay window itself
        cachedMotionOverlay ??= VideoPlayer.Overlay
            .FindChildren<MotionBoundingBoxOverlay>()
            .FirstOrDefault();

        return cachedMotionOverlay;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            timeUpdateTimer?.Stop();
            timeUpdateTimer = null;
            ComponentDispatcher.ThreadFilterMessage -= OnThreadFilterMessage;
            InputManager.Current.PreProcessInput -= OnPreProcessInput;
            viewModel.CloseRequested -= OnCloseRequested;
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            Closed -= OnWindowClosed;
            viewModel.Dispose();
        }

        disposed = true;
    }

    private void OnCloseRequested(
        object? sender,
        DialogClosedEventArgs e)
        => Close();

    private void OnWindowClosed(
        object? sender,
        EventArgs e)
        => Dispose();

    private void OnPreProcessInput(
        object sender,
        PreProcessInputEventArgs e)
    {
        // Skip if disposed or not active
        if (disposed || !IsActive)
        {
            return;
        }

        try
        {
            switch (e.StagingItem.Input)
            {
                case MouseButtonEventArgs mouseButtonArgs:
                    HandleMouseButtonInput(mouseButtonArgs);
                    break;
                case MouseEventArgs mouseArgs:
                    HandleMouseInput(mouseArgs);
                    break;
            }
        }
        catch
        {
            // Silently ignore any errors to avoid interfering with other windows
        }
    }

    private void HandleMouseInput(MouseEventArgs e)
    {
        var currentPosition = e.GetPosition(this);

        if (currentPosition == lastMousePosition)
        {
            return;
        }

        lastMousePosition = currentPosition;
        viewModel.OnMouseMoved();

        // Update selection rectangle while dragging
        if (isSelecting && e.LeftButton == MouseButtonState.Pressed)
        {
            var x = Math.Min(selectionStartPoint.X, currentPosition.X);
            var y = Math.Min(selectionStartPoint.Y, currentPosition.Y);
            var w = Math.Abs(currentPosition.X - selectionStartPoint.X);
            var h = Math.Abs(currentPosition.Y - selectionStartPoint.Y);

            Canvas.SetLeft(ZoomSelectionRect, x);
            Canvas.SetTop(ZoomSelectionRect, y);
            ZoomSelectionRect.Width = w;
            ZoomSelectionRect.Height = h;
        }
        else if (isSelecting)
        {
            isSelecting = false;
            ZoomSelectionCanvas.Visibility = Visibility.Collapsed;
        }

        // Pan while dragging with Ctrl held
        if (isPanning && e.LeftButton == MouseButtonState.Pressed)
        {
            var dx = (float)((panStartScreen.X - currentPosition.X) / ActualWidth) * 2f;
            var dy = (float)((panStartScreen.Y - currentPosition.Y) / ActualHeight) * 2f;

            currentPanX = Math.Clamp(panStartX + dx, -1f, 1f);
            currentPanY = Math.Clamp(panStartY + dy, -1f, 1f);

            VideoPlayer.SetZoom(currentZoom, currentPanX, currentPanY);
        }
        else if (isPanning)
        {
            isPanning = false;
        }
    }

    private void HandleMouseButtonInput(MouseButtonEventArgs e)
    {
        // Show context menu on right-click release
        if (e is { ChangedButton: MouseButton.Right, ButtonState: MouseButtonState.Released })
        {
            ShowContextMenu();
        }

        // Ctrl+Left-click = start pan (when zoomed) or rectangle selection (when not zoomed)
        if (e is { ChangedButton: MouseButton.Left, ButtonState: MouseButtonState.Pressed }
            && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (currentZoom > 1.01f)
            {
                // Zoomed: Ctrl+drag = pan
                isPanning = true;
                panStartScreen = e.GetPosition(this);
                panStartX = currentPanX;
                panStartY = currentPanY;
            }
            else
            {
                // Not zoomed: Ctrl+drag = rectangle selection zoom
                isSelecting = true;
                selectionStartPoint = e.GetPosition(this);
                ZoomSelectionCanvas.Visibility = Visibility.Visible;
                ZoomSelectionRect.Width = 0;
                ZoomSelectionRect.Height = 0;
                Canvas.SetLeft(ZoomSelectionRect, selectionStartPoint.X);
                Canvas.SetTop(ZoomSelectionRect, selectionStartPoint.Y);
            }
        }

        // Left button up
        if (e is { ChangedButton: MouseButton.Left, ButtonState: MouseButtonState.Released })
        {
            if (isPanning)
            {
                isPanning = false;
            }

            if (isSelecting)
            {
                CompleteSelectionZoom(e);
            }
        }

        // Double-click = reset zoom
        if (e is { ChangedButton: MouseButton.Left, ClickCount: 2 } && currentZoom > 1.01f)
        {
            currentZoom = 1.0f;
            currentPanX = 0f;
            currentPanY = 0f;
            VideoPlayer.ResetZoom();
            UpdateMotionOverlayForZoom();
        }
    }

    private void OnThreadFilterMessage(
        ref MSG msg,
        ref bool handled)
    {
        // Skip if disposed, already handled, or not active
        if (disposed || handled || !IsActive)
        {
            return;
        }

        try
        {
            switch (msg.message)
            {
                // Handle ESC key: if zoomed, reset zoom first; otherwise close window
                case WmKeyDown when (int)msg.wParam == VkEscape:
                    if (currentZoom > 1.01f)
                    {
                        currentZoom = 1.0f;
                        currentPanX = 0f;
                        currentPanY = 0f;
                        VideoPlayer.ResetZoom();
                        UpdateMotionOverlayForZoom();
                    }
                    else
                    {
                        viewModel.CloseCommand.Execute(parameter: null);
                    }

                    handled = true;
                    break;

                // Handle Ctrl+Scroll for zoom toward mouse pointer
                case WmMouseWheel:
                {
                    var keys = (short)((int)msg.wParam & 0xFFFF);
                    var isCtrl = (keys & MkControl) != 0;
                    if (isCtrl)
                    {
                        var oldZoom = currentZoom;
                        var delta = (short)((int)msg.wParam >> 16);
                        var factor = delta > 0 ? 1.15f : 0.87f;
                        var newZoom = Math.Clamp(currentZoom * factor, 1.0f, 10.0f);

                        // Mouse coords are screen-relative in WM_MOUSEWHEEL lParam
                        var screenX = (short)(msg.lParam.ToInt32() & 0xFFFF);
                        var screenY = (short)(msg.lParam.ToInt32() >> 16);
                        var clientPoint = PointFromScreen(new Point(screenX, screenY));

                        // Adjust pan so the point under the mouse stays fixed
                        if (newZoom > 1.01f && ActualWidth > 0 && ActualHeight > 0)
                        {
                            var mx = (float)(clientPoint.X / ActualWidth);
                            var my = (float)(clientPoint.Y / ActualHeight);
                            var mxNorm = (mx - 0.5f) * 2f;
                            var myNorm = (my - 0.5f) * 2f;

                            currentPanX += mxNorm * (1f - (oldZoom / newZoom));
                            currentPanY += myNorm * (1f - (oldZoom / newZoom));
                            currentPanX = Math.Clamp(currentPanX, -1f, 1f);
                            currentPanY = Math.Clamp(currentPanY, -1f, 1f);
                        }

                        currentZoom = newZoom;

                        if (currentZoom <= 1.01f)
                        {
                            currentZoom = 1.0f;
                            currentPanX = 0f;
                            currentPanY = 0f;
                        }

                        VideoPlayer.SetZoom(currentZoom, currentPanX, currentPanY);
                        UpdateMotionOverlayForZoom();
                        handled = true;
                    }

                    break;
                }

                // Handle right-click to show context menu
                case WmRightButtonUp:
                    ShowContextMenu();
                    handled = true;
                    break;
            }
        }
        catch
        {
            // Silently ignore any errors to avoid interfering with other windows
        }
    }

    private void ShowContextMenu()
    {
        // Create context menu dynamically to avoid binding conflicts
        var contextMenu = new ContextMenu();

        var resetZoomItem = new MenuItem
        {
            Header = Translations.ResetZoom,
            IsEnabled = currentZoom > 1.01f,
        };

        resetZoomItem.Click += (_, _) =>
        {
            currentZoom = 1.0f;
            currentPanX = 0f;
            currentPanY = 0f;
            VideoPlayer.ResetZoom();
            UpdateMotionOverlayForZoom();
        };

        contextMenu.Items.Add(resetZoomItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(new MenuItem
        {
            Header = Translations.Close,
            Command = viewModel.CloseCommand,
        });
        contextMenu.IsOpen = true;
    }
}