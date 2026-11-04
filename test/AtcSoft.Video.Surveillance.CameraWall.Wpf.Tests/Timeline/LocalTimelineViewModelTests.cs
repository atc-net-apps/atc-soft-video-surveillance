namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Timeline;

using ActivitySummaryCache = AtcSoft.Video.Surveillance.Helpers.Activity.ActivitySummaryCache;
using IActiveRecordings = AtcSoft.Video.Surveillance.Services.IActiveRecordings;
using TimelineCamera = AtcSoft.Video.Surveillance.Models.TimelineCamera;

/// <summary>
/// CameraWall's timeline: a lane per camera from its own folders over the last 1 to 24 hours or a
/// day, play all inside the window, and what play all and a recording's end play next.
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

    [Fact]
    public void It_Offers_The_Last_1_To_24_Hours_And_A_Day_And_Starts_With_24_Hours()
    {
        using var viewModel = new LocalTimelineViewModel(() => [garden], () => [root], recordings, new ActivitySummaryCache(), TimeZoneInfo.Utc);

        viewModel.SpanChoices.Select(c => c.Hours).Should().Equal(1, 3, 6, 12, 24, 0);
        viewModel.SelectedSpan.Hours.Should().Be(24);
        viewModel.IsDayChosen.Should().BeFalse();
    }

    [Fact]
    public async Task The_Last_Hour_Leaves_Out_What_Was_Recorded_Before_It()
    {
        Record("Garden", DateTimeOffset.UtcNow.AddHours(-3), TimeSpan.FromMinutes(10));
        var recent = Record("Garden", DateTimeOffset.UtcNow.AddMinutes(-30), TimeSpan.FromMinutes(10));
        using var viewModel = new LocalTimelineViewModel(() => [garden], () => [root], recordings, new ActivitySummaryCache(), TimeZoneInfo.Utc);

        viewModel.SelectedSpan = viewModel.SpanChoices.First(c => c.Hours == 1);
        await viewModel.LoadCommand.ExecuteAsync(parameter: null);

        viewModel.Lanes[0].Coverage.Select(c => c.VideoPath).Should().Equal(recent);
        viewModel.RangeMaximum.Should().Be(60);
        viewModel.RangeEnd.Should().Be(60, "the whole hour is shown until a thumb moves");
    }

    [Fact]
    public async Task Play_All_Shows_Inside_The_Window_And_Closes_Again()
    {
        Record("Garden", DateTimeOffset.UtcNow.AddHours(-2), TimeSpan.FromMinutes(10));
        using var viewModel = new LocalTimelineViewModel(() => [garden], () => [root], recordings, new ActivitySummaryCache(), TimeZoneInfo.Utc);
        await viewModel.LoadCommand.ExecuteAsync(parameter: null);

        viewModel.PlayAllCommand.Execute(parameter: null);

        viewModel.IsPlayingAll.Should().BeTrue();
        viewModel.PlayAllSession!.Tiles.Should().ContainSingle();
        viewModel.PlayAllMaximum.Should().BeApproximately(24 * 3600, 60);

        viewModel.ClosePlayAllCommand.Execute(parameter: null);

        viewModel.IsPlayingAll.Should().BeFalse();
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