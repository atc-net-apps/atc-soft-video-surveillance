namespace AtcSoft.CameraWall.Wpf.App;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// </summary>
public partial class MainWindow : Fluent.IRibbonWindow
{
    /// <summary>
    /// Gets the title bar. Returns null since NiceWindow doesn't use Fluent.Ribbon's title bar.
    /// Implementing IRibbonWindow prevents binding warnings from Ribbon's internal FindAncestor bindings.
    /// </summary>
    public Fluent.RibbonTitleBar? TitleBar => null;

    private readonly MainWindowViewModel viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The view model.</param>
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();

        this.viewModel = viewModel;
        DataContext = viewModel;

        Loaded += OnLoaded;
        Closing += OnClosing;
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
    }

    private void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        viewModel.OnLoaded(this, e);
        viewModel.Initialize(CameraGridControl);
    }

    private void OnClosing(
        object? sender,
        CancelEventArgs e)
        => viewModel.OnClosing(this, e);

    private void OnKeyDown(
        object sender,
        KeyEventArgs e)
        => viewModel.OnKeyDown(this, e);

    private void OnKeyUp(
        object sender,
        KeyEventArgs e)
        => viewModel.OnKeyUp(this, e);

    private void OnBackstageIsOpenChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        // Collapse the CameraGrid when Backstage opens so VideoHost's
        // IsVisibleChanged fires and hides the native DComp surface windows.
        CameraGridControl.Visibility = (bool)e.NewValue
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void CameraGrid_FullScreenRequested(
        object? sender,
        FullScreenRequestedEventArgs e)
    {
        viewModel.ShowFullScreen(e.Camera, e.SourceTile);
    }

    private void CameraGrid_ConnectionStateChanged(
        object? sender,
        CameraConnectionChangedEventArgs e)
    {
        viewModel.OnConnectionStateChanged(e);
    }

    private void CameraGrid_PositionChanged(
        object? sender,
        CameraPositionChangedEventArgs e)
    {
        viewModel.OnPositionChanged(e);
    }

    private void CameraGrid_EditCameraRequested(
        object? sender,
        CameraConfiguration e)
    {
        viewModel.EditCamera(e);
    }

    private void CameraGrid_DeleteCameraRequested(
        object? sender,
        CameraConfiguration e)
    {
        viewModel.DeleteCamera(e);
    }

    private void CameraGrid_CameraConfigurationChanged(
        object? sender,
        CameraConfiguration e)
    {
        viewModel.SaveCameraConfiguration(e);
    }
}