namespace AtcSoft.Video.Surveillance.Wpf.Dialogs;

/// <summary>
/// Interaction logic for HubCameraDialog: a camera of a camera hub, added through the server.
/// </summary>
public partial class HubCameraDialog
{
    public HubCameraDialog(HubCameraDialogViewModel viewModel)
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