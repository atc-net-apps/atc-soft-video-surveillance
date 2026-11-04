namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;

/// <summary>
/// Shows a <see cref="DialogQuestion"/> over the dialog it belongs to. Put it last in the dialog's
/// root grid, spanning every row and column, with the question as its data context.
/// </summary>
public partial class DialogQuestionOverlay
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DialogQuestionOverlay"/> class.
    /// </summary>
    public DialogQuestionOverlay()
        => InitializeComponent();
}