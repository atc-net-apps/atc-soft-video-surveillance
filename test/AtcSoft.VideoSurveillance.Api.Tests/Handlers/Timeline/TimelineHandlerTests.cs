namespace AtcSoft.VideoSurveillance.Api.Handlers.Timeline;

using ApiTimeline = global::VideoSurveillance.Generated.Timeline.Models.Timeline;
using TimelineGap = global::VideoSurveillance.Generated.Timeline.Models.TimelineGap;

/// <summary>
/// The server's timeline of every camera: lanes over a span with coverage from the recordings,
/// stretches and events from their activity indexes, and the next or previous event from a moment.
/// </summary>
public sealed class TimelineHandlerTests : IDisposable
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-timeline-handler-" + Guid.NewGuid().ToString("N"));
    private readonly IRecordingService recordingService = Substitute.For<IRecordingService>();
    private readonly IApplicationSettingsService settingsService = Substitute.For<IApplicationSettingsService>();
    private readonly ICameraStorageService cameraStorageService = Substitute.For<ICameraStorageService>();
    private readonly ActivitySummaryCache cache = new();
    private readonly IAvailabilityHistory availabilityHistory = Substitute.For<IAvailabilityHistory>();
    private readonly CameraConfiguration garden = Camera("Garden");
    private readonly CameraConfiguration porch = Camera("Porch");

    public TimelineHandlerTests()
    {
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = root });
        recordingService.GetActiveSessions().Returns([]);
        cameraStorageService.GetAllCameras().Returns([garden, porch]);
        availabilityHistory.For(Arg.Any<Guid>()).Returns([]);

        // Garden: recorded 9:00 to 9:10, someone at 9:05; and 11:00 to 11:10, someone at 11:02.
        Record("Garden_20261019_090000_000.mkv", Nine, TimeSpan.FromMinutes(10), eventAt: 300);
        Record("Garden_20261019_110000_000.mkv", Nine.AddHours(2), TimeSpan.FromMinutes(10), eventAt: 120);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Every_Camera_Gets_A_Lane_With_Its_Coverage_Stretches_And_Events()
    {
        var timeline = await TimelineAsync(Nine, Nine.AddHours(1), buckets: 6);

        timeline.Lanes.Select(l => l.CameraName).Should().Equal("Garden", "Porch");

        var lane = timeline.Lanes[0];
        lane.Coverage.Should().ContainSingle().Which.Should().Be(new TimelineCoverage(Nine, Nine.AddMinutes(10), "Garden/Garden_20261019_090000_000.mkv", Nine));
        lane.Buckets.Select(b => (b.IsRecorded, b.IsAnalysed)).Should().Equal(
            (true, true), (false, false), (false, false), (false, false), (false, false), (false, false));
        lane.Buckets[0].Tracks.Should().Be(1);
        lane.Events.Should().ContainSingle().Which.Should().Be(
            new TimelineEvent(garden.Id, Nine.AddSeconds(300), Nine.AddSeconds(303), 25, 1, "Garden/Garden_20261019_090000_000.mkv", 300));

        timeline.Lanes[1].Buckets.Should().OnlyContain(b => !b.IsRecorded, "the porch recorded nothing");
        timeline.Lanes[1].Events.Should().BeEmpty();
    }

    [Fact]
    public async Task A_Gap_Tells_Why_Nothing_Was_Recorded_From_The_Cameras_History()
    {
        availabilityHistory.For(garden.Id).Returns(
        [
            new AvailabilityEntry(Nine.AddMinutes(-5), AvailabilityChange.Online),
            new AvailabilityEntry(Nine.AddMinutes(-5), AvailabilityChange.RecordingStarted),
            new AvailabilityEntry(Nine.AddMinutes(10), AvailabilityChange.Offline),
            new AvailabilityEntry(Nine.AddMinutes(50), AvailabilityChange.Online),
            new AvailabilityEntry(Nine.AddMinutes(50), AvailabilityChange.DiskFull),
        ]);

        var timeline = await TimelineAsync(Nine, Nine.AddHours(1), buckets: 6);

        timeline.Lanes[0].Gaps.Should().Equal(
            new TimelineGap(Nine.AddMinutes(10), Nine.AddMinutes(50), TimelineGapReason.Offline),
            new TimelineGap(Nine.AddMinutes(50), Nine.AddHours(1), TimelineGapReason.DiskFull));
        timeline.Lanes[1].Gaps.Should().Equal(
            [new TimelineGap(Nine, Nine.AddHours(1), TimelineGapReason.Unknown)],
            "the porch has no history");
    }

    [Fact]
    public async Task One_Camera_Can_Be_Asked_For()
    {
        var result = await GetTimelineAsync(Nine, Nine.AddHours(3), buckets: 3, cameraId: porch.Id);

        result.Result.Should().BeOfType<Ok<ApiTimeline>>().Subject.Value!.Lanes.Should().ContainSingle().Which.CameraId.Should().Be(porch.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    [InlineData((7 * 24 * 60) + 1)]
    public async Task A_Span_That_Is_Empty_Backwards_Or_Longer_Than_A_Week_Is_Refused(
        int minutes)
    {
        var result = await GetTimelineAsync(Nine, Nine.AddMinutes(minutes), buckets: 10);

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Next_And_Previous_Find_The_Nearest_Events_Across_Recordings()
    {
        var next = await EventsAsync(after: Nine.AddMinutes(6), before: null, limit: null);
        var previous = await EventsAsync(after: null, before: Nine.AddHours(2), limit: null);
        var both = await EventsAsync(after: Nine.AddHours(-1), before: null, limit: 5);

        next.Should().ContainSingle().Which.Start.Should().Be(Nine.AddHours(2).AddSeconds(120));
        previous.Should().ContainSingle().Which.Start.Should().Be(Nine.AddSeconds(300));
        both.Select(e => e.Start).Should().Equal(Nine.AddSeconds(300), Nine.AddHours(2).AddSeconds(120));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Next_Or_Previous_Needs_Exactly_One_Moment(
        bool after,
        bool before)
    {
        var handler = new ListTimelineEventsHandler(recordingService, settingsService, cameraStorageService, cache, TimeProvider.System);

        var result = await handler.ExecuteAsync(
            new ListTimelineEventsParameters(After: after ? Nine : null, Before: before ? Nine : null, CameraId: null, Limit: null),
            TestContext.Current.CancellationToken);

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private static CameraConfiguration Camera(string name)
    {
        var camera = new CameraConfiguration();
        camera.Display.DisplayName = name;
        return camera;
    }

    private void Record(
        string fileName,
        DateTimeOffset start,
        TimeSpan length,
        int eventAt)
    {
        var video = Path.Combine(root, "Garden", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(video)!);
        File.WriteAllText(video, "video");
        File.SetLastWriteTimeUtc(video, (start + length).UtcDateTime);

        using var index = ActivityLogWriter.Create(RecordingPathHelper.BuildActivityPath(video), start);
        for (var second = 0; second < (int)length.TotalSeconds; second += 1)
        {
            var moving = second >= eventAt && second < eventAt + 3;
            index.Add(
                start.AddSeconds(second),
                new MotionDetectorResult(moving, MotionHoldChange.None, moving ? 25 : 1, moving ? [new BoundingBox { Width = 5, Height = 5, TrackId = 1 }] : []));
        }
    }

    private async Task<ApiTimeline> TimelineAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        int buckets)
    {
        var result = await GetTimelineAsync(from, to, buckets, cameraId: null);
        return result.Result.Should().BeOfType<Ok<ApiTimeline>>().Subject.Value!;
    }

    private Task<global::VideoSurveillance.Generated.Timeline.Results.GetTimelineResult> GetTimelineAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        int buckets,
        Guid? cameraId = null)
        => new GetTimelineHandler(recordingService, settingsService, cameraStorageService, cache, TimeProvider.System, availabilityHistory).ExecuteAsync(
            new GetTimelineParameters(From: from, To: to, CameraId: cameraId, Buckets: buckets),
            TestContext.Current.CancellationToken);

    private async Task<List<TimelineEvent>> EventsAsync(
        DateTimeOffset? after,
        DateTimeOffset? before,
        int? limit)
    {
        var result = await new ListTimelineEventsHandler(recordingService, settingsService, cameraStorageService, cache, TimeProvider.System).ExecuteAsync(
            new ListTimelineEventsParameters(After: after, Before: before, CameraId: null, Limit: limit),
            TestContext.Current.CancellationToken);
        return result.Result.Should().BeOfType<Ok<List<TimelineEvent>>>().Subject.Value!;
    }
}