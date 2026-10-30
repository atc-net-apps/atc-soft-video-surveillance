namespace AtcSoft.VideoSurveillance.Wpf.Windows;

/// <summary>
/// Fullscreen window for displaying a single camera stream.
/// </summary>
public partial class FullScreenCameraWindow : IDisposable
{
    private const int WmKeyDown = 0x0100;
    private const int VkEscape = 0x1B;

    private readonly FullScreenCameraWindowViewModel viewModel;
    private MotionBoundingBoxOverlay? cachedMotionOverlay;
    private Point lastMousePosition;
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
    }

    private void OnViewModelPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
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

        // The picture's size as shown (rotation applied) for letterbox-aware coordinate mapping
        if (viewModel.MotionVideoWidth > 0 && viewModel.MotionVideoHeight > 0)
        {
            motionOverlay.VideoWidth = viewModel.MotionVideoWidth;
            motionOverlay.VideoHeight = viewModel.MotionVideoHeight;
        }

        // Get the video container size for coordinate mapping
        var containerSize = new Size(VideoPlayer.ActualWidth, VideoPlayer.ActualHeight);
        if ((containerSize.Width <= 0 || containerSize.Height <= 0) &&
            VideoPlayer.Overlay is not null)
        {
            containerSize = new Size(VideoPlayer.Overlay.ActualWidth, VideoPlayer.Overlay.ActualHeight);
        }

        motionOverlay.UpdateMotionBoxes(viewModel.CurrentBoundingBoxes, containerSize);
    }

    private MotionBoundingBoxOverlay? GetMotionBoundingBoxOverlay()
    {
        if (cachedMotionOverlay is not null)
        {
            return cachedMotionOverlay;
        }

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
            cachedMotionOverlay = FindChild<MotionBoundingBoxOverlay>(content);
        }

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
        if (disposed || !IsActive)
        {
            return;
        }

        try
        {
            if (e.StagingItem.Input is MouseEventArgs mouseArgs)
            {
                HandleMouseInput(mouseArgs);
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
    }

    private void OnThreadFilterMessage(
        ref MSG msg,
        ref bool handled)
    {
        if (disposed || handled || !IsActive)
        {
            return;
        }

        try
        {
            // Handle ESC key at Win32 message level
            if (msg.message == WmKeyDown && (int)msg.wParam == VkEscape)
            {
                viewModel.CloseCommand.Execute(parameter: null);
                handled = true;
            }
        }
        catch
        {
            // Silently ignore any errors to avoid interfering with other windows
        }
    }

    private static T? FindChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found)
            {
                return found;
            }

            var result = FindChild<T>(child);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }
}