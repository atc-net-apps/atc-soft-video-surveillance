namespace AtcSoft.VideoSurveillance.Wpf.Dialogs;

/// <summary>
/// Interaction logic for SignInDialog.
/// </summary>
public partial class SignInDialog
{
    public SignInDialog(SignInDialogViewModel viewModel)
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