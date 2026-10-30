namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

using IWpfCoreSettingsService = AtcSoft.VideoSurveillance.Wpf.Core.Services.IApplicationSettingsService;

/// <summary>
/// The browser can be reopened the way it was left: same filters, same
/// recording selected.
/// </summary>
public sealed class RecordingsBrowserDialogViewModelStateTests : IDisposable
{
    private readonly string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-browser-state-" + Guid.NewGuid().ToString("N"));

    public RecordingsBrowserDialogViewModelStateTests()
    {
        Touch("Front Door", "Front Door_20261012_101500_000.mkv");
        Touch("Garden", "Garden_20261012_090000_000.mkv");
        Touch("Garden", "Garden_20261012_220000_000.mkv");
        Touch("Garden", "Garden_20261012_230000_000.mkv");
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(root))
        {
            TestFolders.Delete(root);
        }
    }

    [Fact]
    public void A_Reopened_Browser_Has_The_Filters_And_The_Selection_It_Was_Left_With()
    {
        // Arrange
        using var first = CreateViewModel();
        first.SelectedCameraFilter = "Garden";
        first.SelectedDayFilter = "_ALL_";
        first.SelectedTimeFilter = "18_24";
        first.SelectedRecording = first.FilteredRecordings.Last();
        var state = first.CaptureState();

        // Act
        using var reopened = CreateViewModel();
        reopened.RestoreState(state);

        // Assert
        reopened.SelectedCameraFilter.Should().Be("Garden");
        reopened.SelectedDayFilter.Should().Be("_ALL_");
        reopened.SelectedTimeFilter.Should().Be("18_24");
        reopened.FilteredRecordings.Select(r => r.FileName).Should().Equal(
            "Garden_20261012_230000_000.mkv",
            "Garden_20261012_220000_000.mkv");
        reopened.SelectedRecording!.FileName.Should().Be("Garden_20261012_220000_000.mkv");
        reopened.SelectedRecording.Should().BeSameAs(reopened.FilteredRecordings.Last(), "the grid selects by instance");
    }

    [Fact]
    public void A_Camera_That_Has_No_Recordings_Any_More_Falls_Back_To_All_Cameras()
    {
        using var first = CreateViewModel();
        first.SelectedCameraFilter = "Front Door";
        first.SelectedDayFilter = "_ALL_";
        first.SelectedRecording = first.FilteredRecordings.Single();
        var state = first.CaptureState();

        System.IO.Directory.Delete(System.IO.Path.Combine(root, "Front Door"), recursive: true);

        using var reopened = CreateViewModel();
        reopened.RestoreState(state);

        reopened.SelectedCameraFilter.Should().Be("_ALL_");
        reopened.SelectedRecording.Should().BeNull("the recording is gone");
        reopened.FilteredRecordings.Should().HaveCount(3);
    }

    private RecordingsBrowserDialogViewModel CreateViewModel()
    {
        var settingsService = Substitute.For<IWpfCoreSettingsService>();
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = root });
        return new RecordingsBrowserDialogViewModel(settingsService);
    }

    private void Touch(params string[] segments)
    {
        var path = System.IO.Path.Combine([root, .. segments]);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, "x");
    }
}