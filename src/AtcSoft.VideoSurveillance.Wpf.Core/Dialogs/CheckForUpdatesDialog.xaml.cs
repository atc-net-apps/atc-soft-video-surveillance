namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

/// <summary>
/// Dialog for checking for application updates.
/// </summary>
public partial class CheckForUpdatesDialog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckForUpdatesDialog"/> class.
    /// </summary>
    /// <param name="viewModel">The view model.</param>
    public CheckForUpdatesDialog(CheckForUpdatesDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;

        viewModel.CloseRequested += (_, e) =>
        {
            DialogResult = e.DialogResult;
            Close();
        };

        // Start checking for updates when the dialog loads
        Loaded += async (_, _) =>
        {
            await viewModel
                .CheckForUpdatesAsync()
                .ConfigureAwait(true);
        };
    }
}