namespace AtcSoft.VideoSurveillance.Wpf.Helpers;

/// <summary>
/// Thin convenience wrapper around <see cref="InfoDialogBox"/> /
/// <see cref="QuestionDialogBox"/> for ad-hoc call sites that don't
/// have an injected dialog service. Replaces the previous
/// <c>MessageBox.Show</c> usage so the app's themed dialog style is
/// consistent across every error / confirmation popup.
/// </summary>
internal static class UserDialog
{
    public static void ShowError(
        string message,
        string? title = null)
        => Show(
            message,
            title ?? Translations.Error,
            new DialogBoxSettings(DialogBoxType.Ok, LogCategoryType.Error));

    public static void ShowWarning(
        string message,
        string? title = null)
        => Show(
            message,
            title ?? Translations.Warning,
            new DialogBoxSettings(DialogBoxType.Ok, LogCategoryType.Warning));

    public static void ShowInfo(
        string message,
        string? title = null)
        => Show(
            message,
            title ?? Translations.Information,
            DialogBoxSettings.Create(DialogBoxType.Ok));

    /// <summary>
    /// Shows a Yes / No question dialog. Returns <see langword="true"/>
    /// for Yes, <see langword="false"/> for No / dialog dismissed.
    /// </summary>
    public static bool Confirm(
        string message,
        string title)
    {
        var owner = ResolveOwner();
        if (owner is null)
        {
            return false;
        }

        var dialog = new QuestionDialogBox(owner, title, message)
        {
            Width = 400,
        };
        return dialog.ShowDialog() == true;
    }

    private static void Show(
        string message,
        string title,
        DialogBoxSettings settings)
    {
        var owner = ResolveOwner();
        if (owner is null)
        {
            return;
        }

        settings.TitleBarText = title;
        settings.Width = 400;

        var dialog = new InfoDialogBox(owner, settings, message);
        dialog.ShowDialog();
    }

    private static Window? ResolveOwner()
        => Application.Current?.MainWindow
           ?? Application.Current?.Windows.OfType<Window>().FirstOrDefault();
}