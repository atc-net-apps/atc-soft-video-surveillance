namespace AtcSoft.Video.Surveillance.Wpf.Core.Windows;

/// <summary>
/// *Play all from here*: a grid of every camera's recordings from one moment, with one pause for
/// all. Shown in its own window by the desktop client, and inside the timeline window by CameraWall
/// (a dialog opens no other window). A new view model (another moment) starts again.
/// </summary>
public partial class PlayAllView
{
    private int attached;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayAllView"/> class.
    /// </summary>
    public PlayAllView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => attached = 0;
        Loaded += (_, _) =>
        {
            // No tiles: nothing gives the start below.
            if (DataContext is PlayAllWindowViewModel { Tiles.Count: 0 } empty)
            {
                empty.Start();
            }
        };
    }

    // The clock starts once every tile has its player.
    private void OnMediaLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is MediaElement { DataContext: PlayAllTileViewModel tile } element &&
            DataContext is PlayAllWindowViewModel viewModel)
        {
            tile.Attach(new MediaElementPlayAllMedia(element));
            if (++attached == viewModel.Tiles.Count)
            {
                viewModel.Start();
            }
        }
    }
}