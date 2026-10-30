namespace AtcSoft.VideoSurveillance.Wpf.Views;

/// <summary>
/// Interaction logic for LayoutListView.
/// </summary>
public partial class LayoutListView
{
    public LayoutListView()
    {
        InitializeComponent();
    }

    private void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is LayoutListViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(parameter: null);
        }
    }
}