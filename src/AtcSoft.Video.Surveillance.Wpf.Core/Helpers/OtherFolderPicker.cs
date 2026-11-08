namespace AtcSoft.Video.Surveillance.Wpf.Core.Helpers;

/// <summary>
/// The system's folder picker for another CameraWall's folder (a share such as <c>\\pc\CameraWall</c>),
/// over the window that asks for it.
/// </summary>
public static class OtherFolderPicker
{
    /// <summary>
    /// Asks for a folder.
    /// </summary>
    /// <returns>The folder chosen, or <see langword="null"/> when none was.</returns>
    public static string? Pick()
    {
        var dialog = new OpenFolderDialog
        {
            Title = Translations.OpenAnotherFolder,
            Multiselect = false,
        };
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Application.Current?.MainWindow;
        return (owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) == true
            ? dialog.FolderName
            : null;
    }
}