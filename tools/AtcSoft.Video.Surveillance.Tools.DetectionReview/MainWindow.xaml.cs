namespace AtcSoft.Video.Surveillance.Tools.DetectionReview;

/// <summary>
/// The review window. Only the mouse is handled here: positions on the picture are in the video's
/// pixels, since the picture is drawn at its own size inside a Viewbox.
/// </summary>
public partial class MainWindow
{
    private readonly MainWindowViewModel viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnSurfaceMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        Surface.CaptureMouse();
        viewModel.BeginDraw(e.GetPosition(Surface));
        e.Handled = true;
    }

    private void OnSurfaceMouseMove(
        object sender,
        MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            viewModel.MoveDraw(Clamp(e.GetPosition(Surface)));
        }
    }

    private void OnSurfaceMouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs e)
    {
        Surface.ReleaseMouseCapture();
        viewModel.EndDraw(Clamp(e.GetPosition(Surface)));
        e.Handled = true;
    }

    private void OnPersonBoxRightClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BoxView box })
        {
            viewModel.Remove(box);
            e.Handled = true;
        }
    }

    // A drag that leaves the picture ends at its edge.
    private Point Clamp(Point point)
        => new(Math.Clamp(point.X, 0, Surface.ActualWidth), Math.Clamp(point.Y, 0, Surface.ActualHeight));
}