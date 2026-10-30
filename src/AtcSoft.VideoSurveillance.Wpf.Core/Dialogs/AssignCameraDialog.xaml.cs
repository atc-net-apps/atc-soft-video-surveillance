namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

/// <summary>
/// Dialog for assigning/unassigning cameras to the current layout.
/// </summary>
public partial class AssignCameraDialog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssignCameraDialog"/> class.
    /// </summary>
    /// <param name="viewModel">The view model.</param>
    public AssignCameraDialog(AssignCameraDialogViewModel viewModel)
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