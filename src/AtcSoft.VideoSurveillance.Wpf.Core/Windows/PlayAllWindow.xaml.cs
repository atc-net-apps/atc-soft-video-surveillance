namespace AtcSoft.VideoSurveillance.Wpf.Core.Windows;

/// <summary>
/// *Play all from here*: a grid of every camera's recordings from one moment.
/// </summary>
public partial class PlayAllWindow
{
    private readonly PlayAllWindowViewModel viewModel;
    private int attached;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayAllWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The tiles and their pacer; disposed when the window closes.</param>
    public PlayAllWindow(PlayAllWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Closed += (_, _) => viewModel.Dispose();
        Loaded += (_, _) =>
        {
            // No tiles: nothing gives the start below.
            if (viewModel.Tiles.Count == 0)
            {
                viewModel.Start();
            }
        };
    }

    // The clock starts once every tile has its player.
    private void OnMediaLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is MediaElement { DataContext: PlayAllTileViewModel tile } element)
        {
            tile.Attach(new MediaElementPlayAllMedia(element));
            if (++attached == viewModel.Tiles.Count)
            {
                viewModel.Start();
            }
        }
    }
}