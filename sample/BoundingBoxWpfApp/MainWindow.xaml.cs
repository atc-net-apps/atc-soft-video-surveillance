namespace BoundingBoxWpfApp;

/// <summary>
/// The sample's window.
/// </summary>
public partial class MainWindow
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}