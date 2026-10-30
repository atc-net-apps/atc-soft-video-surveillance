namespace AtcSoft.VideoSurveillance.Wpf.Views;

/// <summary>
/// Interaction logic for LiveView.
/// </summary>
public partial class LiveView
{
    public LiveView()
    {
        InitializeComponent();
    }

    private void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is LiveViewViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(parameter: null);
        }
    }

    private void OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is LiveViewViewModel viewModel)
        {
            viewModel.StopAllCommand.Execute(parameter: null);
        }
    }
}