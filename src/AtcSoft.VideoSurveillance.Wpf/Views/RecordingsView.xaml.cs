namespace AtcSoft.VideoSurveillance.Wpf.Views;

/// <summary>
/// Interaction logic for RecordingsView.
/// </summary>
public partial class RecordingsView
{
    public RecordingsView()
    {
        InitializeComponent();
    }

    private void OnLoaded(
        object sender,
        RoutedEventArgs e)
    {
        if (DataContext is RecordingsViewModel viewModel)
        {
            viewModel.LoadCommand.Execute(parameter: null);
        }
    }
}