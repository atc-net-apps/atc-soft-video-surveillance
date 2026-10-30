namespace AtcSoft.VideoSurveillance.Api.Handlers.Recordings;

/// <summary>
/// A client asks what moved when in a recording, for its timeline. The answer comes from the
/// activity index beside the recording, shrunk to the timeline's width.
/// </summary>
public sealed class GetRecordingActivityHandlerTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 19, 10, 15, 0, TimeSpan.Zero);

    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-recording-activity-" + Guid.NewGuid().ToString("N"));
    private readonly IApplicationSettingsService settingsService = Substitute.For<IApplicationSettingsService>();
    private readonly GetRecordingActivityHandler sut;
    private readonly string video;

    public GetRecordingActivityHandlerTests()
    {
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = root });
        video = Path.Combine(root, "Garden", "Garden_20261019_101500_000.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(video)!);
        File.WriteAllText(video, "x");

        sut = new GetRecordingActivityHandler(settingsService);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task The_Activity_Is_Shrunk_To_The_Buckets_Asked_For_Keeping_The_Most_Of_Each()
    {
        using (var writer = ActivityLogWriter.Create(RecordingPathHelper.BuildActivityPath(video), Start))
        {
            for (var second = 0; second < 10; second++)
            {
                writer.Add(Start.AddSeconds(second), second == 7 ? Moving(30, trackId: 4) : Quiet(1));
            }
        }

        var activity = await OkAsync("Garden/Garden_20261019_101500_000.mp4", buckets: 5, durationSeconds: 10);

        activity.HasIndex.Should().BeTrue();
        activity.StartedAt.Should().Be(Start);
        activity.DurationSeconds.Should().Be(10);
        activity.Buckets.Select(b => b.Level).Should().Equal(1, 1, 1, 30, 1);
        activity.Buckets.Select(b => b.Tracks).Should().Equal(0, 0, 0, 1, 0);
        activity.Buckets.Should().OnlyContain(b => b.IsAnalysed);
    }

    [Fact]
    public async Task A_Stretch_Names_The_Watch_Zones_Something_Moved_In_And_Leaves_Them_Out_Otherwise()
    {
        using (var writer = ActivityLogWriter.Create(RecordingPathHelper.BuildActivityPath(video), Start))
        {
            writer.Add(Start, Moving(30, trackId: 4, zone: "Gate"));
            writer.Add(Start.AddSeconds(1), Moving(30, trackId: 4));
        }

        var activity = await OkAsync("Garden/Garden_20261019_101500_000.mp4", buckets: 2, durationSeconds: 2);

        activity.Buckets[0].Zones.Should().Equal("Gate");
        activity.Buckets[1].Zones.Should().BeNull("no zone is left out, not an empty list");
    }

    [Fact]
    public async Task Without_A_Length_The_Buckets_Cover_The_Index_As_Far_As_It_Reaches()
    {
        using (var writer = ActivityLogWriter.Create(RecordingPathHelper.BuildActivityPath(video), Start))
        {
            writer.Add(Start, Quiet(2));
            writer.Add(Start.AddSeconds(3), Quiet(6));
        }

        var activity = await OkAsync("Garden/Garden_20261019_101500_000.mp4", buckets: 4, durationSeconds: null);

        activity.DurationSeconds.Should().Be(4);
        activity.Buckets.Select(b => b.IsAnalysed).Should().Equal(true, false, false, true);
    }

    [Fact]
    public async Task A_Recording_Without_An_Index_Says_So()
    {
        var activity = await OkAsync("Garden/Garden_20261019_101500_000.mp4", buckets: null, durationSeconds: 60);

        activity.HasIndex.Should().BeFalse();
        activity.Buckets.Should().BeEmpty();
        activity.StartedAt.Should().BeNull();
    }

    [Fact]
    public async Task A_Recording_Being_Analysed_Says_So_And_One_Whose_Analysis_Ended_Without_An_Index_Does_Not()
    {
        var analyses = Substitute.For<IRecordingAnalyses>();
        analyses.Contains(video).Returns(true);
        var handler = new GetRecordingActivityHandler(settingsService, analyses);

        var going = await handler.ExecuteAsync(new GetRecordingActivityParameters(FilePath: "Garden/Garden_20261019_101500_000.mp4", Buckets: null, DurationSeconds: null), TestContext.Current.CancellationToken);
        analyses.Contains(video).Returns(false);
        var ended = await handler.ExecuteAsync(new GetRecordingActivityParameters(FilePath: "Garden/Garden_20261019_101500_000.mp4", Buckets: null, DurationSeconds: null), TestContext.Current.CancellationToken);

        going.Result.Should().BeOfType<Ok<RecordingActivity>>().Subject.Value!.IsBeingAnalysed.Should().BeTrue();
        var failed = ended.Result.Should().BeOfType<Ok<RecordingActivity>>().Subject.Value!;
        (failed.HasIndex || failed.IsBeingAnalysed).Should().BeFalse("a client waiting for it stops and says the analysis failed");
    }

    [Fact]
    public async Task A_Recording_That_Is_Not_There_Is_Not_Found()
    {
        var result = await ExecuteAsync("Garden/Garden_20261019_111500_000.mp4", buckets: null, durationSeconds: null);

        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Theory]
    [InlineData("../Garden_20261019_101500_000.mp4")]
    [InlineData("Garden/Garden_20261019_101500_000.activity.jsonl")]
    public async Task A_Path_That_Does_Not_Name_A_Recording_Is_Refused(
        string filePath)
    {
        var result = await ExecuteAsync(filePath, buckets: null, durationSeconds: null);

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private static MotionDetectorResult Quiet(double level)
        => new(false, MotionHoldChange.None, level, []);

    private static MotionDetectorResult Moving(
        double level,
        int trackId,
        string? zone = null)
        => new(true, MotionHoldChange.Continues, level, [new BoundingBox { X = 1, Y = 1, Width = 5, Height = 5, TrackId = trackId, Zone = zone }]);

    private async Task<RecordingActivity> OkAsync(
        string filePath,
        int? buckets,
        double? durationSeconds)
    {
        var result = await ExecuteAsync(filePath, buckets, durationSeconds);
        return result.Result.Should().BeOfType<Ok<RecordingActivity>>().Subject.Value!;
    }

    private Task<global::VideoSurveillance.Generated.Recordings.Results.GetRecordingActivityResult> ExecuteAsync(
        string filePath,
        int? buckets,
        double? durationSeconds)
        => sut.ExecuteAsync(
            new GetRecordingActivityParameters(FilePath: filePath, Buckets: buckets, DurationSeconds: durationSeconds),
            TestContext.Current.CancellationToken);
}