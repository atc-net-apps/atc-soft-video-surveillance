namespace AtcSoft.VideoSurveillance.Wpf.Views;

/// <summary>
/// Interaction logic for DashboardView.
/// </summary>
public partial class DashboardView
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(parameter: null);
        }
    }
}