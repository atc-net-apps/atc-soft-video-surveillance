namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Timeline;

using ActivitySummaryCache = AtcSoft.Video.Surveillance.Helpers.Activity.ActivitySummaryCache;
using AvailabilityChange = AtcSoft.Video.Surveillance.Enums.AvailabilityChange;
using AvailabilityEntry = AtcSoft.Video.Surveillance.Models.AvailabilityEntry;
using AvailabilityFolder = AtcSoft.Video.Surveillance.Helpers.Activity.AvailabilityFolder;
using AvailabilityLog = AtcSoft.Video.Surveillance.Helpers.Activity.AvailabilityLog;
using GapReason = AtcSoft.Video.Surveillance.Enums.GapReason;
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
        viewModel.Zoom.Maximum.Should().Be(60);
        viewModel.Zoom.End.Should().Be(60, "the whole hour is shown until a thumb moves");
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

    [Fact]
    public async Task A_Camera_That_Dropped_Out_Has_A_Red_Gap_With_Its_Times_And_A_Summary_By_Its_Name()
    {
        var availability = Path.Combine(root, "availability");
        var started = DateTimeOffset.UtcNow.AddHours(-5);
        AvailabilityLog.Append(Path.Combine(availability, AvailabilityFolder.AppFileName), new AvailabilityEntry(started, AvailabilityChange.ServerStarted));
        AvailabilityLog.Append(AvailabilityFolder.CameraFile(availability, garden.Id), new AvailabilityEntry(started.AddMinutes(1), AvailabilityChange.Online));
        AvailabilityLog.Append(AvailabilityFolder.CameraFile(availability, garden.Id), new AvailabilityEntry(started.AddHours(1), AvailabilityChange.Offline));
        AvailabilityLog.Append(AvailabilityFolder.CameraFile(availability, garden.Id), new AvailabilityEntry(started.AddHours(2).AddMinutes(30), AvailabilityChange.Online));
        AvailabilityLog.Append(AvailabilityFolder.CameraFile(availability, porch.Id), new AvailabilityEntry(started.AddMinutes(1), AvailabilityChange.Online));
        using var viewModel = new LocalTimelineViewModel(
            () => [garden, porch],
            () => [root],
            recordings,
            new ActivitySummaryCache(),
            TimeZoneInfo.Utc,
            availabilityFolder: availability,
            folderHistory: new RecordingFolderHistory(Path.Combine(root, "folders.json")));

        await viewModel.LoadCommand.ExecuteAsync(parameter: null);

        var lane = viewModel.Lanes[0];
        lane.GapBands.Should().Contain(g => g.Reason == GapReason.Offline && g.Text!.Contains("1 h 30 min", StringComparison.Ordinal));
        lane.HasOffline.Should().BeTrue();
        lane.OfflineText.Should().Contain("1×").And.Contain("1 h 30 min");
        viewModel.Lanes[1].HasOffline.Should().BeFalse();
        viewModel.Lanes[1].GapBands.Should().NotContain(g => g.Reason == GapReason.Offline);
    }

    [Fact]
    public async Task Another_CameraWalls_Folder_Is_Shown_Remembered_And_Left_Again()
    {
        var shared = Path.Combine(root, "shared");
        var other = new TimelineCamera(Guid.NewGuid(), "Yard");
        Directory.CreateDirectory(Path.Combine(shared, "recordings", "Yard"));
        await File.WriteAllTextAsync(Path.Combine(shared, "cameras.json"), $$"""{ "cameras": [ { "id": "{{other.Id}}", "display": { "displayName": "Yard" } } ] }""", TestContext.Current.CancellationToken);
        var history = new RecordingFolderHistory(Path.Combine(root, "folders.json"));
        var picked = shared;
        using var viewModel = new LocalTimelineViewModel(
            () => [garden],
            () => [root],
            recordings,
            new ActivitySummaryCache(),
            TimeZoneInfo.Utc,
            pickFolder: () => picked,
            folderHistory: history);
        var busy = new List<(bool Busy, string Text)>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LocalTimelineViewModel.IsReadingOtherFolder))
            {
                busy.Add((viewModel.IsReadingOtherFolder, viewModel.ReadingOtherFolderText));
            }
        };

        await viewModel.OpenFolderCommand.ExecuteAsync(parameter: null);

        // The window says it reads the folder while it does, and stops saying so.
        busy.Should().Contain((true, $"Opening {shared}..."));
        viewModel.IsReadingOtherFolder.Should().BeFalse();
        viewModel.IsExternal.Should().BeTrue();
        viewModel.Lanes.Select(l => l.CameraName).Should().Equal("Yard");
        viewModel.SelectedSource.Folder.Should().Be(shared);
        history.Read().Should().Equal(shared);

        picked = Path.Combine(root, "not-there");
        await viewModel.OpenFolderCommand.ExecuteAsync(parameter: null);
        viewModel.StatusMessage.Should().Contain("not-there");
        viewModel.SelectedSource.Folder.Should().Be(shared, "a folder that does not open leaves what was shown");

        viewModel.SelectedSource = RecordingSourceChoice.ThisComputer;
        await viewModel.LoadCommand.ExecuteAsync(parameter: null);
        viewModel.IsExternal.Should().BeFalse();
        viewModel.Lanes.Select(l => l.CameraName).Should().Equal("Garden");
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