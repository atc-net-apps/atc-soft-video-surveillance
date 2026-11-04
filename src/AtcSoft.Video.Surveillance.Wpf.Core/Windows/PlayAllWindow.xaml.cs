namespace AtcSoft.Video.Surveillance.Wpf.Core.Windows;

/// <summary>
/// *Play all from here* in a window of its own (<see cref="PlayAllView"/>), for an app's main
/// window to open; a dialog shows the view inside itself instead.
/// </summary>
public partial class PlayAllWindow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlayAllWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The tiles and their pacer; disposed when the window closes.</param>
    public PlayAllWindow(PlayAllWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
        Closed += (_, _) => viewModel.Dispose();
    }
}