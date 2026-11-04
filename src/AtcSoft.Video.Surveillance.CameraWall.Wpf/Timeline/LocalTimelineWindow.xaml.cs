namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Timeline;

/// <summary>
/// CameraWall's timeline of every camera. It grows while it is open, and lets go of the recording
/// service's events when it closes. *Play all from here* shows over the lanes; its slider moves
/// through the time shown, and plays all from where it is let go.
/// </summary>
public partial class LocalTimelineWindow
{
    private readonly LocalTimelineViewModel viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalTimelineWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The lanes and their commands; disposed when the window closes.</param>
    public LocalTimelineWindow(LocalTimelineViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) =>
        {
            viewModel.StartLive();
            viewModel.LoadCommand.Execute(parameter: null);
        };
        Closed += (_, _) => viewModel.Dispose();
    }

    // Only a move the person makes zooms (TimelineZoomRange).
    private void OnZoomSliderTouched(
        object sender,
        RoutedEventArgs e)
        => viewModel.Zoom.Touch();

    private void OnPlayAllSliderDown(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
        => viewModel.BeginSeek();

    private void OnPlayAllSliderUp(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
        => viewModel.EndSeek();
}