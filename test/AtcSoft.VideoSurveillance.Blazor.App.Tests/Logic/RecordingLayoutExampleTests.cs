namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

using CoreLayoutOptions = AtcSoft.VideoSurveillance.Helpers.RecordingLayoutOptions;
using CorePathHelper = AtcSoft.VideoSurveillance.Helpers.RecordingPathHelper;

/// <summary>
/// The web client does not reference Core, so it has its own copy of how a
/// recording path looks. These tests keep the copy equal to the original.
/// </summary>
public sealed class RecordingLayoutExampleTests
{
    private static readonly DateTime When = new(2026, 10, 13, 10, 15, 0, DateTimeKind.Unspecified);

    [Theory]
    [InlineData(true, true, "mkv")]
    [InlineData(true, false, "mkv")]
    [InlineData(false, true, "mp4")]
    [InlineData(false, false, "avi")]
    public void The_Example_Is_What_The_Server_Would_Write(
        bool useDailySubfolders,
        bool separateThumbnailFolder,
        string format)
    {
        var expected = CorePathHelper.BuildExample(
            "Cam1",
            When,
            format,
            new CoreLayoutOptions(useDailySubfolders, separateThumbnailFolder));

        var actual = RecordingLayoutExample.Build("Cam1", When, format, useDailySubfolders, separateThumbnailFolder);

        actual.Video.Should().Be(expected.Video);
        actual.Thumbnail.Should().Be(expected.Thumbnail);
    }

    [Fact]
    public void Both_On_Reads_As_Day_Folders_And_A_Thumbnails_Folder()
    {
        var example = RecordingLayoutExample.Build("Cam1", When, "mkv", useDailySubfolders: true, separateThumbnailFolder: true);

        example.Video.Should().Be(@"Cam1\2026-10-13\Cam1_20261013_101500_000.mkv");
        example.Thumbnail.Should().Be(@"thumbnails\Cam1\2026-10-13\Cam1_20261013_101500_000.png");
    }
}