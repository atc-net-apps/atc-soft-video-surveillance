namespace AtcSoft.VideoSurveillance.Wpf.Dialogs;

/// <summary>
/// Interaction logic for ChangePasswordDialog.
/// </summary>
public partial class ChangePasswordDialog
{
    public ChangePasswordDialog(ChangePasswordDialogViewModel viewModel)
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