namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

/// <summary>
/// Dialog for configuring a camera (add or edit).
/// </summary>
public partial class CameraConfigurationDialog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CameraConfigurationDialog"/> class.
    /// </summary>
    /// <param name="viewModel">The view model.</param>
    public CameraConfigurationDialog(
        CameraConfigurationDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;

        viewModel.CloseRequested += (_, e) =>
        {
            DialogResult = e.DialogResult;
            Close();
        };
    }
}