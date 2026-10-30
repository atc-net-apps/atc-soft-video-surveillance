namespace AtcSoft.VideoSurveillance.Wpf.Views;

/// <summary>
/// Interaction logic for CameraListView.
/// </summary>
public partial class CameraListView
{
    public CameraListView()
    {
        InitializeComponent();
    }

    private void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is CameraListViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(parameter: null);
        }
    }
}