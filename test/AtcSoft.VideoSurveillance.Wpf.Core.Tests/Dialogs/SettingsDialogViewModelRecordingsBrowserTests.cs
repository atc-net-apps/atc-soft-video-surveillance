namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

using ApplicationSettingsService = AtcSoft.VideoSurveillance.Wpf.Core.Services.ApplicationSettingsService;

public sealed class SettingsDialogViewModelRecordingsBrowserTests : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-settings-browser-" + Guid.NewGuid().ToString("N"));
    private readonly ApplicationSettingsService settingsService;

    public SettingsDialogViewModelRecordingsBrowserTests()
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
    public void Remembering_The_Recordings_Browser_Is_On_By_Default()
    {
        new RecordingSettings().RememberRecordingsBrowserState.Should().BeTrue();
        new SettingsDialogViewModel(settingsService).RememberRecordingsBrowserState.Should().BeTrue();
    }

    [Fact]
    public async Task Switching_It_Off_Is_Saved_And_Shown_Again()
    {
        var sut = new SettingsDialogViewModel(settingsService)
        {
            RememberRecordingsBrowserState = false,
        };

        await sut.SaveAsync();

        settingsService.Recording.RememberRecordingsBrowserState.Should().BeFalse();
        new SettingsDialogViewModel(settingsService).RememberRecordingsBrowserState.Should().BeFalse();
    }

    [Fact]
    public void Closing_The_Recordings_Browser_On_Play_Is_Off_By_Default()
    {
        new RecordingSettings().CloseRecordingsBrowserOnPlay.Should().BeFalse();
        new SettingsDialogViewModel(settingsService).CloseRecordingsBrowserOnPlay.Should().BeFalse();
    }

    [Fact]
    public async Task Switching_Close_On_Play_On_Is_Saved_And_Shown_Again()
    {
        var sut = new SettingsDialogViewModel(settingsService)
        {
            CloseRecordingsBrowserOnPlay = true,
        };

        await sut.SaveAsync();

        settingsService.Recording.CloseRecordingsBrowserOnPlay.Should().BeTrue();
        new SettingsDialogViewModel(settingsService).CloseRecordingsBrowserOnPlay.Should().BeTrue();
    }
}