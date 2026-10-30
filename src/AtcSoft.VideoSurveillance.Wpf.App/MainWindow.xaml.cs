namespace AtcSoft.VideoSurveillance.Wpf.App;

/// <summary>
/// Interaction logic for MainWindow.xaml.
/// </summary>
public partial class MainWindow : Fluent.IRibbonWindow
{
    private readonly MainWindowViewModel viewModel;
    private readonly WindowStateService windowStateService;

    /// <summary>
    /// Gets the title bar. Returns null since NiceWindow doesn't use Fluent.Ribbon's title bar.
    /// Implementing IRibbonWindow prevents binding warnings from Ribbon's internal FindAncestor bindings.
    /// </summary>
    public Fluent.RibbonTitleBar? TitleBar => null;

    public MainWindow(MainWindowViewModel viewModel)
    {
        this.viewModel = viewModel;

        windowStateService = new WindowStateService();
        windowStateService.Load();
        windowStateService.ApplyTo(this);

        InitializeComponent();
        DataContext = viewModel;

        Closing += OnClosing;
    }

    private void OnBackstageIsOpenChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        // Collapse the main content when Backstage opens so VideoHost's
        // IsVisibleChanged fires and hides the native DComp surface windows.
        MainContent.Visibility = (bool)e.NewValue
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnClosing(
        object? sender,
        CancelEventArgs e)
    {
        // Restore from full screen before capturing state
        if (viewModel.IsFullScreen)
        {
            viewModel.RestoreFromFullScreen(this);
        }

        windowStateService.CaptureFrom(this);
        windowStateService.Save();
    }
}