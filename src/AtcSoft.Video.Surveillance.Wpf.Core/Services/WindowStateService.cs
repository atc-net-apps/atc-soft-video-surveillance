namespace AtcSoft.Video.Surveillance.Wpf.Core.Services;

/// <summary>
/// Persists and restores a main window's position, size, and state, in a file of the app's own.
/// Not DI-registered — each app's MainWindow makes one with its file.
/// </summary>
public sealed class WindowStateService : JsonFileServiceBase<WindowStateData>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WindowStateService"/> class.
    /// </summary>
    /// <param name="filePath">The file the state is kept in, from <c>ApplicationPaths.GetLocalDataPath</c>.</param>
    public WindowStateService(string filePath)
        : base(filePath)
    {
    }

    /// <summary>
    /// Gets whether saved state was loaded successfully.
    /// </summary>
    public bool HasSavedState { get; private set; }

    /// <summary>
    /// Applies saved state to the window. Validates position against virtual screen bounds.
    /// </summary>
    public void ApplyTo(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.Width = Data.Width;
        window.Height = Data.Height;

        if (HasSavedState)
        {
            // Validate position is within current virtual screen bounds
            var left = Data.Left;
            var top = Data.Top;
            var virtualLeft = SystemParameters.VirtualScreenLeft;
            var virtualTop = SystemParameters.VirtualScreenTop;
            var virtualWidth = SystemParameters.VirtualScreenWidth;
            var virtualHeight = SystemParameters.VirtualScreenHeight;

            if (left >= virtualLeft &&
                top >= virtualTop &&
                left + Data.Width <= virtualLeft + virtualWidth &&
                top + Data.Height <= virtualTop + virtualHeight)
            {
                window.Left = left;
                window.Top = top;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (Data.IsMaximized)
        {
            // Defer maximization to after window is shown to preserve RestoreBounds
            window.Loaded += (_, _) => window.WindowState = WindowState.Maximized;
        }
    }

    /// <summary>
    /// Captures current window state. Uses RestoreBounds when maximized to preserve normal-state dimensions.
    /// </summary>
    public void CaptureFrom(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        Data.IsMaximized = window.WindowState == WindowState.Maximized;

        if (Data.IsMaximized)
        {
            // Use RestoreBounds to capture the normal-state position/size
            var bounds = window.RestoreBounds;
            Data.Left = bounds.Left;
            Data.Top = bounds.Top;
            Data.Width = bounds.Width;
            Data.Height = bounds.Height;
        }
        else
        {
            Data.Left = window.Left;
            Data.Top = window.Top;
            Data.Width = window.Width;
            Data.Height = window.Height;
        }
    }

    protected override void OnLoaded()
        => HasSavedState = !double.IsNaN(Data.Left) && !double.IsNaN(Data.Top);
}