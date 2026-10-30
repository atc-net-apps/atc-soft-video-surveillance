namespace AtcSoft.CameraWall.Wpf.Timeline;

using ActivitySummaryCache = AtcSoft.VideoSurveillance.Helpers.Activity.ActivitySummaryCache;
using IActiveRecordings = AtcSoft.VideoSurveillance.Services.IActiveRecordings;
using TimelineCamera = AtcSoft.VideoSurveillance.Models.TimelineCamera;

/// <summary>
/// CameraWall's timeline: a lane per camera from its own folders, and what play all and a
/// recording's end play next.
/// </summary>
public sealed class LocalTimelineViewModelTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-camerawall-timeline-" + Guid.NewGuid().ToString("N"));
    private readonly IActiveRecordings recordings = Substitute.For<IActiveRecordings>();
    private readonly TimelineCamera garden = new(Guid.NewGuid(), "Garden");
    private readonly TimelineCamera porch = new(Guid.NewGuid(), "Porch");

    public LocalTimelineViewModelTests()
        => recordings.GetActiveSessions().Returns([]);

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Every_Camera_Gets_A_Lane_From_The_Recordings_Of_The_Last_24_Hours()
    {
        var started = DateTimeOffset.UtcNow.AddHours(-2);
        var first = Record("Garden", started, TimeSpan.FromMinutes(10));
        var second = Record("Garden", started.AddMinutes(30), TimeSpan.FromMinutes(10));
        using var viewModel = new LocalTimelineViewModel(() => [garden, porch], () => [root], recordings, new ActivitySummaryCache(), TimeZoneInfo.Utc);

        await viewModel.LoadCommand.ExecuteAsync(parameter: null);

        viewModel.Lanes.Select(l => l.CameraName).Should().Equal("Garden", "Porch");
        var lane = viewModel.Lanes[0];
        lane.Coverage.Select(c => c.VideoPath).Should().Equal(first, second);
        lane.Stretches.Should().Contain(s => s.IsRecorded);
        viewModel.Lanes[1].Coverage.Should().BeEmpty();
        viewModel.Marks.Should().NotBeEmpty();

        var next = LocalTimelineViewModel.NextSegments(lane, first);
        next()!.FilePath.Should().Be(second);
        next().Should().BeNull();

        var tiles = LocalTimelineViewModel.PlayAllTiles(viewModel.Lanes, started.AddMinutes(20));
        tiles.Should().ContainSingle().Which.CameraName.Should().Be("Garden");
    }

    private string Record(
        string camera,
        DateTimeOffset start,
        TimeSpan length)
    {
        var video = Path.Combine(root, camera, $"{camera}_{start.UtcDateTime:yyyyMMdd_HHmmss}_000.mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(video)!);
        File.WriteAllText(video, "video");
        File.SetLastWriteTimeUtc(video, (start + length).UtcDateTime);
        return video;
    }
}