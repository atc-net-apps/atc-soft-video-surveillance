namespace AtcSoft.VideoSurveillance.Wpf.Dialogs;

/// <summary>
/// Interaction logic for CameraEditDialog.
/// </summary>
public partial class CameraEditDialog
{
    public CameraEditDialog(CameraEditDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;

        viewModel.CloseRequested += (_, args) =>
        {
            DialogResult = args.DialogResult;
            Close();
        };
    }
}