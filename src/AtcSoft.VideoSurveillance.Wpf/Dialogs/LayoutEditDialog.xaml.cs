namespace AtcSoft.VideoSurveillance.Wpf.Dialogs;

/// <summary>
/// Interaction logic for LayoutEditDialog.
/// </summary>
public partial class LayoutEditDialog
{
    public LayoutEditDialog(LayoutEditDialogViewModel viewModel)
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