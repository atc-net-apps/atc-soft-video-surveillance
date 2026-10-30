namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

/// <summary>
/// Dialog shown at start when the FFmpeg the engine needs is not found.
/// </summary>
public partial class FFmpegSetupDialog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FFmpegSetupDialog"/> class.
    /// </summary>
    /// <param name="viewModel">The view model.</param>
    public FFmpegSetupDialog(FFmpegSetupDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;

        viewModel.CloseRequested += (_, e) =>
        {
            DialogResult = e.DialogResult;
            Close();
        };

        // Closing the window while FFmpeg downloads stops the download.
        Closing += (_, _) => viewModel.CancelInstallCommand.Execute(null);
    }
}