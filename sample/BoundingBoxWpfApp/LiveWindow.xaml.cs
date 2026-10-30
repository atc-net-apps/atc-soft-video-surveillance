namespace BoundingBoxWpfApp;

/// <summary>
/// The live clock's window (<see cref="LiveWindowViewModel"/>).
/// </summary>
public partial class LiveWindow
{
    private readonly LiveWindowViewModel viewModel;

    public LiveWindow(LiveWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await viewModel.StartAsync().ConfigureAwait(true);
    }

    private void OnClosed(
        object? sender,
        EventArgs e)
        => viewModel.Dispose();
}