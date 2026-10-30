namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

using ApplicationSettingsService = AtcSoft.VideoSurveillance.Wpf.Core.Services.ApplicationSettingsService;

/// <summary>
/// The grid's size is chosen in the main window, not in the settings
/// dialog. Saving the dialog must not put it back to automatic.
/// </summary>
public sealed class SettingsDialogViewModelGridSizeTests : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-settings-grid-" + Guid.NewGuid().ToString("N"));
    private readonly ApplicationSettingsService settingsService;

    public SettingsDialogViewModelGridSizeTests()
    {
        System.IO.Directory.CreateDirectory(directory);
        settingsService = new ApplicationSettingsService(System.IO.Path.Combine(directory, "settings.json"));
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(directory))
        {
            TestFolders.Delete(directory);
        }
    }

    [Fact]
    public async Task Saving_The_Settings_Dialog_Keeps_The_Grid_Size_Chosen_In_The_Main_Window()
    {
        var general = settingsService.General.Clone();
        general.GridSize = 2;
        settingsService.SaveGeneral(general);

        await new SettingsDialogViewModel(settingsService).SaveAsync();

        settingsService.General.GridSize.Should().Be(2);
        new ApplicationSettingsService(System.IO.Path.Combine(directory, "settings.json")).General.GridSize
            .Should().Be(2, "it is still there after a restart");
    }
}