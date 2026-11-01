namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;

using IWpfCoreSettingsService = AtcSoft.Video.Surveillance.Wpf.Core.Services.IApplicationSettingsService;

/// <summary>
/// The browser reads an archive that keeps changing (recordings start and end, the cleanup deletes
/// the oldest). What cannot be read at that moment is left out or shown without its extras; the
/// list is still shown, and the app does not stop.
/// </summary>
public sealed class RecordingsBrowserDialogViewModelUnreadableTests : IDisposable
{
    private readonly string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-browser-unreadable-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (System.IO.Directory.Exists(root))
        {
            TestFolders.Delete(root);
        }
    }

    [Fact]
    public async Task A_Recording_Whose_Activity_Index_Is_Locked_Is_Listed_Without_Its_Activity()
    {
        var video = Touch("Front Door", "Front Door_20261012_101500_000.mkv");
        Touch("Garden", "Garden_20261012_111500_000.mkv");
        var index = AtcSoft.Video.Surveillance.Helpers.RecordingPathHelper.BuildActivityPath(video);
        await System.IO.File.WriteAllTextAsync(index, "{}", TestContext.Current.CancellationToken);

        using (new System.IO.FileStream(index, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None))
        {
            using var viewModel = Create();
            await viewModel.Loading;

            viewModel.FilteredRecordings.Select(r => r.CameraName).Should().BeEquivalentTo("Front Door", "Garden");
            viewModel.FilteredRecordings.Single(r => r.CameraName == "Front Door").Activity.Should().BeNull();
        }
    }

    [Fact]
    public async Task A_Recording_Folder_That_Is_Gone_Gives_An_Empty_List()
    {
        using var viewModel = Create();
        await viewModel.Loading;

        viewModel.FilteredRecordings.Should().BeEmpty();
        viewModel.IsLoading.Should().BeFalse();
        (await viewModel.UpdateOngoingRecordingsAsync()).Should().BeFalse();
    }

    private RecordingsBrowserDialogViewModel Create()
    {
        var settingsService = Substitute.For<IWpfCoreSettingsService>();
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = root });
        return new RecordingsBrowserDialogViewModel(settingsService);
    }

    private string Touch(params string[] segments)
    {
        var path = System.IO.Path.Combine([root, .. segments]);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, "x");
        return path;
    }
}