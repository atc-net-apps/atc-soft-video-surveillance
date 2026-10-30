namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

using ApplicationSettingsService = AtcSoft.VideoSurveillance.Wpf.Core.Services.ApplicationSettingsService;

/// <summary>
/// Whether recordings are made as H.264 whatever the camera sends was a
/// setting in the file with nowhere to set it.
/// </summary>
public sealed class SettingsDialogViewModelTranscodeTests : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-settings-transcode-" + Guid.NewGuid().ToString("N"));
    private readonly ApplicationSettingsService settingsService;

    public SettingsDialogViewModelTranscodeTests()
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
    public void Recordings_Keep_The_Cameras_Video_By_Default()
    {
        var sut = new SettingsDialogViewModel(settingsService);

        sut.SelectedTranscodeVideoCodec.Should().Be("None");
        sut.TranscodeVideoCodecItems.Keys.Should().Equal("None", "H264");
    }

    [Fact]
    public async Task Choosing_H264_Is_Saved_And_Shown_Again()
    {
        var sut = new SettingsDialogViewModel(settingsService)
        {
            SelectedTranscodeVideoCodec = "H264",
        };

        await sut.SaveAsync();

        settingsService.Recording.TranscodeVideoCodec.Should().Be(VideoTranscodeCodec.H264);
        new SettingsDialogViewModel(settingsService).SelectedTranscodeVideoCodec.Should().Be("H264");
    }

    [Fact]
    public async Task Going_Back_To_The_Cameras_Video_Is_Saved_Too()
    {
        settingsService.Recording.TranscodeVideoCodec = VideoTranscodeCodec.H264;
        var sut = new SettingsDialogViewModel(settingsService)
        {
            SelectedTranscodeVideoCodec = "None",
        };

        await sut.SaveAsync();

        settingsService.Recording.TranscodeVideoCodec.Should().Be(VideoTranscodeCodec.None);
    }
}