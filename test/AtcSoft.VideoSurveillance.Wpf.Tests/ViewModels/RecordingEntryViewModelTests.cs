namespace AtcSoft.VideoSurveillance.Wpf.ViewModels;

using ApiRecording = global::VideoSurveillance.Generated.Recordings.Models.Recording;

public class RecordingEntryViewModelTests
{
    private static readonly Uri ApiBaseUrl = new("http://server:5000/api");

    [Fact]
    public void PlaybackUrl_Points_At_The_Recording_In_A_Day_Folder()
    {
        var vm = Build("Front Door/2026-10-12/Front Door_20261012_101500_000.mkv");

        vm.PlaybackUrl.AbsoluteUri.Should().Be(
            "http://server:5000/recordings-files/Front%20Door/2026-10-12/Front%20Door_20261012_101500_000.mkv");
    }

    [Theory]
    [InlineData("Cam #1/Cam #1_20261012_101500_000.mkv", "/recordings-files/Cam%20%231/Cam%20%231_20261012_101500_000.mkv")]
    [InlineData("50% Gate/a.mkv", "/recordings-files/50%25%20Gate/a.mkv")]
    [InlineData("Door & Yard/a.mkv", "/recordings-files/Door%20%26%20Yard/a.mkv")]
    [InlineData("A+B/a.mkv", "/recordings-files/A%2BB/a.mkv")]
    public void PlaybackUrl_Escapes_Characters_That_Would_Change_The_Url(
        string filePath,
        string expectedPath)
    {
        var vm = Build(filePath);

        // A '#' would otherwise start a fragment and cut the path short.
        vm.PlaybackUrl.Fragment.Should().BeEmpty();
        vm.PlaybackUrl.Query.Should().BeEmpty();
        vm.PlaybackUrl.AbsolutePath.Should().Be(expectedPath);
    }

    [Fact]
    public void ThumbnailUrl_Points_At_The_Thumbnail_The_Server_Reported()
    {
        var vm = Build(
            "Front Door/2026-10-12/Front Door_20261012_101500_000.mkv",
            "thumbnails/Front Door/2026-10-12/Front Door_20261012_101500_000.png");

        vm.ThumbnailUrl!.AbsoluteUri.Should().Be(
            "http://server:5000/recordings-files/thumbnails/Front%20Door/2026-10-12/Front%20Door_20261012_101500_000.png");
    }

    [Fact]
    public void ThumbnailUrl_Is_Null_For_A_Recording_Without_A_Thumbnail()
    {
        var vm = Build("Front Door/a.mkv");

        vm.ThumbnailUrl.Should().BeNull();
    }

    [Fact]
    public void The_Media_Token_Is_Added_To_Both_Addresses()
    {
        var vm = Build("Front Door/a.mkv", "thumbnails/Front Door/a.png", mediaToken: "token+/=");

        vm.PlaybackUrl.Query.Should().Be("?media=token%2B%2F%3D");
        vm.ThumbnailUrl!.Query.Should().Be("?media=token%2B%2F%3D");
    }

    private static RecordingEntryViewModel Build(
        string filePath,
        string? thumbnailPath = null,
        string? mediaToken = null)
        => new(
            new ApiRecording(
                Id: Guid.NewGuid(),
                CameraId: Guid.NewGuid(),
                CameraName: "Cam",
                FilePath: filePath,
                StartedAt: DateTimeOffset.UnixEpoch,
                Duration: string.Empty,
                FileSizeBytes: 0,
                HasThumbnail: thumbnailPath is not null,
                InProgress: false,
                ThumbnailPath: thumbnailPath),
            ApiBaseUrl,
            mediaToken);
}