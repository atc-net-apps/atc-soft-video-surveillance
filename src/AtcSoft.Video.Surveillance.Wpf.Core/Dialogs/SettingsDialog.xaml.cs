namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;

/// <summary>
/// Dialog for application settings.
/// </summary>
public partial class SettingsDialog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsDialog"/> class.
    /// </summary>
    /// <param name="viewModel">The view model.</param>
    public SettingsDialog(SettingsDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;

        var closed = false;
        Closed += (_, _) => closed = true;

        // While recordings are moved to a new folder layout the dialog stays open (the title bar's
        // close button, Esc and Alt+F4 included); it closes by itself when the move is done.
        Closing += (_, e) => e.Cancel |= viewModel.IsApplyingRecordingLayout;

        viewModel.CloseRequested += (_, e) =>
        {
            if (closed)
            {
                return;
            }

            DialogResult = e.DialogResult;
            Close();
        };
    }
}