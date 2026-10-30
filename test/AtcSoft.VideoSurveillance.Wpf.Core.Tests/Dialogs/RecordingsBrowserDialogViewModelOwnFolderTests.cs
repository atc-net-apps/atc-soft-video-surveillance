namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

using IWpfCoreSettingsService = AtcSoft.VideoSurveillance.Wpf.Core.Services.IApplicationSettingsService;

/// <summary>
/// A camera can record into a folder of its own. The browser lists those recordings too
/// (before, it read only the global folder, so they never showed), and deletes them there.
/// </summary>
public sealed class RecordingsBrowserDialogViewModelOwnFolderTests : IDisposable
{
    private readonly string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-own-" + Guid.NewGuid().ToString("N"));

    private string Global
        => System.IO.Path.Combine(root, "global");

    private string Own
        => System.IO.Path.Combine(root, "own");

    public void Dispose()
    {
        if (System.IO.Directory.Exists(root))
        {
            TestFolders.Delete(root);
        }
    }

    [Fact]
    public void Recordings_In_A_Cameras_Own_Folder_Are_Listed()
    {
        Touch(Global, "Front Door", "Front Door_20261012_101500_000.mkv");
        Touch(Own, "Garden", "Garden_20261012_111500_000.mkv");

        using var viewModel = Create();

        viewModel.FilteredRecordings.Select(r => r.CameraName).Should().BeEquivalentTo("Front Door", "Garden");
        viewModel.CameraFilters.Select(f => f.Key).Should().Contain("Garden");
    }

    [Fact]
    public void A_Recording_In_A_Cameras_Own_Folder_Is_Deleted_There()
    {
        var video = Touch(Own, "Garden", "Garden_20261012_111500_000.mkv");
        using var viewModel = Create();
        viewModel.SelectedRecording = viewModel.FilteredRecordings.Single();

        viewModel.DeleteCommand.Execute(null);

        System.IO.File.Exists(video).Should().BeFalse();
        viewModel.FilteredRecordings.Should().BeEmpty();
    }

    private RecordingsBrowserDialogViewModel Create()
    {
        var settingsService = Substitute.For<IWpfCoreSettingsService>();
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = Global });

        return new RecordingsBrowserDialogViewModel(settingsService, () => [Own, null]);
    }

    private static string Touch(params string[] segments)
    {
        var path = System.IO.Path.Combine(segments);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, "x");
        return path;
    }
}