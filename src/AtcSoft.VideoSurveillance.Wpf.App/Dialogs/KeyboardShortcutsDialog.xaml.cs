namespace AtcSoft.VideoSurveillance.Wpf.App.Dialogs;

/// <summary>
/// Read-only reference dialog showing all keyboard shortcuts.
/// </summary>
public partial class KeyboardShortcutsDialog
{
    public KeyboardShortcutsDialog()
    {
        InitializeComponent();

        ShortcutsList.ItemsSource = new[]
        {
            new { Key = "Ctrl+1", Description = Translations.LiveView },
            new { Key = "Ctrl+2", Description = Translations.Dashboard },
            new { Key = "Ctrl+3", Description = Translations.Cameras },
            new { Key = "Ctrl+4", Description = Translations.Layouts },
            new { Key = "Ctrl+5", Description = Translations.Recordings },
            new { Key = "Ctrl+6", Description = Translations.Notifications },
            new { Key = "Ctrl+7", Description = Translations.Timeline },
            new { Key = "Ctrl+N", Description = Translations.ViewCameras },
            new { Key = "Ctrl+,", Description = Translations.Settings },
            new { Key = "F5", Description = Translations.RefreshCurrentView },
            new { Key = "F11", Description = Translations.ToggleFullScreen },
            new { Key = "F1", Description = Translations.KeyboardShortcuts },
        };
    }

    private void OkButton_Click(
        object sender,
        RoutedEventArgs e)
        => Close();
}