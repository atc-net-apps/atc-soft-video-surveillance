namespace AtcSoft.VideoSurveillance.Wpf.Core.Windows;

/// <summary>
/// Fullscreen window for playing back recorded video files.
/// Uses WPF MediaElement for local file playback.
/// </summary>
public partial class FullScreenRecordingWindow : IDisposable
{
    private const int WmKeyDown = 0x0100;
    private const int VkEscape = 0x1B;

    private readonly FullScreenRecordingWindowViewModel viewModel;
    private Point lastMousePosition;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FullScreenRecordingWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The view model.</param>
    public FullScreenRecordingWindow(
        FullScreenRecordingWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        this.viewModel = viewModel;
        DataContext = viewModel;

        // Pass the MediaElement to the ViewModel for playback control
        viewModel.SetMediaElement(VideoPlayer);

        viewModel.CloseRequested += OnCloseRequested;
        Closed += OnWindowClosed;

        // Use InputManager to capture mouse input
        InputManager.Current.PreProcessInput += OnPreProcessInput;

        // Use ComponentDispatcher to capture keyboard at Win32 level
        ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;

        // Setup seek slider event handlers
        SeekSlider.ValueChanged += OnSeekSliderValueChanged;

        // Use Loaded event to get the Thumb for more reliable drag detection
        SeekSlider.Loaded += OnSeekSliderLoaded;
    }

    private void OnSeekSliderLoaded(
        object sender,
        RoutedEventArgs e)
    {
        // Get the Thumb from the slider template for reliable drag events
        var track = SeekSlider.Template.FindName("PART_Track", SeekSlider) as System.Windows.Controls.Primitives.Track;
        if (track?.Thumb is { } thumb)
        {
            thumb.DragStarted += OnSeekThumbDragStarted;
            thumb.DragCompleted += OnSeekThumbDragCompleted;
        }
    }

    private void OnSeekThumbDragStarted(
        object? sender,
        System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        viewModel.OnSeekStarted();
    }

    private void OnSeekThumbDragCompleted(
        object? sender,
        System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        viewModel.OnSeekCompleted();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the window resources.
    /// </summary>
    /// <param name="disposing">Whether managed resources should be disposed.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            SeekSlider.ValueChanged -= OnSeekSliderValueChanged;
            SeekSlider.Loaded -= OnSeekSliderLoaded;

            // Clean up thumb events if available
            var track = SeekSlider.Template?.FindName("PART_Track", SeekSlider) as System.Windows.Controls.Primitives.Track;
            if (track?.Thumb is { } thumb)
            {
                thumb.DragStarted -= OnSeekThumbDragStarted;
                thumb.DragCompleted -= OnSeekThumbDragCompleted;
            }

            ComponentDispatcher.ThreadFilterMessage -= OnThreadFilterMessage;
            InputManager.Current.PreProcessInput -= OnPreProcessInput;
            viewModel.CloseRequested -= OnCloseRequested;
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

    private void OnSeekSliderValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        viewModel.OnSeekValueChanged();
    }
}