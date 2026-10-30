namespace AtcSoft.VideoSurveillance.Wpf.App.Dialogs;

/// <summary>
/// Dialog for selecting or entering a server to connect to.
/// Shown before DI container is built.
/// </summary>
public partial class ServerConnectionDialog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServerConnectionDialog"/> class.
    /// </summary>
    /// <param name="viewModel">The view model.</param>
    public ServerConnectionDialog(ServerConnectionDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;

        viewModel.CloseRequested += (_, result) =>
        {
            DialogResult = result;
            Close();
        };
    }
}