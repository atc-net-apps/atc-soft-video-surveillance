namespace AtcSoft.Video.Surveillance.Wpf.Dialogs;

/// <summary>
/// Interaction logic for UsersDialog.
/// </summary>
public partial class UsersDialog
{
    public UsersDialog(UsersDialogViewModel viewModel)
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

    /// <summary>
    /// Shows the dialog over the owner, asking inside it before anything is removed.
    /// </summary>
    public static void Show(
        IUserAdministration administration,
        Window? owner)
    {
        var viewModel = new UsersDialogViewModel(administration);
        var dialog = new UsersDialog(viewModel)
        {
            Owner = owner,
        };

        viewModel.LoadCommand.Execute(parameter: null);
        dialog.ShowDialog();
    }
}