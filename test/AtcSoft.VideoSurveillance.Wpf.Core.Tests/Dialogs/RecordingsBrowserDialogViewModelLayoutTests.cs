namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

using IWpfCoreSettingsService = AtcSoft.VideoSurveillance.Wpf.Core.Services.IApplicationSettingsService;
using RecordingLayoutOptions = AtcSoft.VideoSurveillance.Helpers.RecordingLayoutOptions;
using RecordingLayoutReorganizer = AtcSoft.VideoSurveillance.Helpers.RecordingLayoutReorganizer;

/// <summary>
/// The recordings browser shows the same list however the files are laid
/// out on disk, so moving them around changes nothing for the user.
/// </summary>
public sealed class RecordingsBrowserDialogViewModelLayoutTests : IDisposable
{
    private static readonly string[] Extensions = [".mkv", ".mp4"];

    private readonly string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-browser-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (System.IO.Directory.Exists(root))
        {
            TestFolders.Delete(root);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void The_List_Is_The_Same_After_The_Files_Are_Moved_To_Another_Layout(
        bool useDailySubfolders,
        bool separateThumbnailFolder)
    {
        // Arrange — two cameras recorded in the original flat layout
        Touch("Front Door", "Front Door_20261011_080000_000.mkv");
        Touch("Front Door", "Front Door_20261011_080000_000.png");
        Touch("Front Door", "Front Door_20261012_101500_000.mkv");
        Touch("Garden", "Garden_20261012_230000_000.mp4");
        Touch("Garden", "Garden_20261012_230000_000.png");

        var before = Snapshot();

        // Act
        var result = RecordingLayoutReorganizer.Reorganize(
            root,
            new RecordingLayoutOptions(useDailySubfolders, separateThumbnailFolder),
            Extensions);

        // Assert
        result.Errors.Should().BeEmpty();
        (result.MovedRecordings + result.MovedThumbnails).Should().BeGreaterThan(0, "the layout did change");
        Snapshot().Should().Equal(before);
    }

    [Fact]
    public void The_List_Is_The_Same_After_The_Files_Are_Moved_Back()
    {
        Touch("Front Door", "2026-10-12", "Front Door_20261012_101500_000.mkv");
        Touch("thumbnails", "Front Door", "2026-10-12", "Front Door_20261012_101500_000.png");

        var before = Snapshot();

        RecordingLayoutReorganizer.Reorganize(root, RecordingLayoutOptions.Flat, Extensions);

        Snapshot().Should().Equal(before);
        before.Should().ContainSingle().Which.Should().Be("Front Door|Front Door_20261012_101500_000.mkv|2026-10-12 10:15:00|thumbnail");
    }

    /// <summary>
    /// What the browser shows for each recording, in its display order.
    /// </summary>
    private List<string> Snapshot()
    {
        var settingsService = Substitute.For<IWpfCoreSettingsService>();
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = root });

        using var viewModel = new RecordingsBrowserDialogViewModel(settingsService);

        return [.. viewModel.FilteredRecordings.Select(r =>
            string.Join(
                '|',
                r.CameraName,
                r.FileName,
                r.RecordingTime.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
                r.HasThumbnail ? "thumbnail" : "no thumbnail"))];
    }

    private void Touch(params string[] segments)
    {
        var path = System.IO.Path.Combine([root, .. segments]);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, "x");
    }
}