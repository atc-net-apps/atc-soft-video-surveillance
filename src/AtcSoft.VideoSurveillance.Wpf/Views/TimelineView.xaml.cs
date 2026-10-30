namespace AtcSoft.VideoSurveillance.Wpf.Views;

/// <summary>
/// Interaction logic for TimelineView.
/// </summary>
public partial class TimelineView
{
    public TimelineView()
    {
        InitializeComponent();
    }

    private void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is TimelineViewModel viewModel)
        {
            viewModel.IsShown = true;
            viewModel.LoadCommand.Execute(parameter: null);
        }
    }

    private void OnUnloaded(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is TimelineViewModel viewModel)
        {
            viewModel.IsShown = false;
        }
    }
}