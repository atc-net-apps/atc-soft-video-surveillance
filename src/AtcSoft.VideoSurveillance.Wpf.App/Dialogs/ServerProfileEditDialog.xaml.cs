namespace AtcSoft.VideoSurveillance.Wpf.App.Dialogs;

/// <summary>
/// Dialog for adding or editing a server profile.
/// </summary>
public partial class ServerProfileEditDialog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServerProfileEditDialog"/> class.
    /// </summary>
    /// <param name="viewModel">The view model.</param>
    public ServerProfileEditDialog(ServerProfileEditDialogViewModel viewModel)
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