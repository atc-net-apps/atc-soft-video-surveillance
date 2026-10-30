namespace AtcSoft.CameraWall.Wpf.Timeline;

/// <summary>
/// CameraWall's timeline of every camera. It grows while it is open, and lets go of the recording
/// service's events when it closes.
/// </summary>
public partial class LocalTimelineWindow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalTimelineWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The lanes and their commands; disposed when the window closes.</param>
    public LocalTimelineWindow(LocalTimelineViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) =>
        {
            viewModel.StartLive();
            viewModel.LoadCommand.Execute(parameter: null);
        };
        Closed += (_, _) => viewModel.Dispose();
    }
}