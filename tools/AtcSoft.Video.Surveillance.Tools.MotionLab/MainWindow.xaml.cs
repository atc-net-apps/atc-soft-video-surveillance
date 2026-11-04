namespace AtcSoft.Video.Surveillance.Tools.MotionLab;

/// <summary>
/// Motion Lab's window.
/// </summary>
public partial class MainWindow
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}