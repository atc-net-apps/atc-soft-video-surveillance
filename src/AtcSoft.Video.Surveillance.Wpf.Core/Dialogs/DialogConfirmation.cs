namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;

/// <summary>
/// Asks a yes/no question inside a dialog, as a flyout in the dialog's own <see cref="FlyoutHost"/>
/// (Atc.Wpf's <see cref="InWindowDialogService"/>), never in a window of its own: a dialog opens no
/// other window. Every question asked here removes something, so Enter answers No.
/// </summary>
public sealed class DialogConfirmation
{
    private readonly FlyoutService flyoutService = new();
    private readonly InWindowDialogService dialogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialogConfirmation"/> class.
    /// </summary>
    public DialogConfirmation()
        => dialogService = new InWindowDialogService(flyoutService);

    /// <summary>
    /// Sets the host the questions are shown in: a <see cref="FlyoutHost"/> last in the dialog's root grid.
    /// </summary>
    /// <param name="host">The dialog's flyout host.</param>
    public void SetHost(FlyoutHost host)
        => flyoutService.SetFlyoutHost(host);

    /// <summary>
    /// Shows the question and waits for the answer.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <returns><see langword="true"/> for yes.</returns>
    public Task<bool> AskAsync(string question)
    {
        var settings = Atc.Wpf.Forms.Dialogs.DialogBoxSettings.Create(Atc.Wpf.Forms.Dialogs.DialogBoxType.YesNo);
        settings.TitleBarText = Translations.Confirm;
        settings.DefaultButton = Atc.Wpf.Forms.Dialogs.DialogBoxButton.Negative;
        return dialogService.ShowConfirmation(settings, question);
    }
}