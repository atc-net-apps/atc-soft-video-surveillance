namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// The activity index of a recording: one line per analysed second, written while the recording
/// is made, read back even while it grows, and shrunk to a timeline's width without losing a
/// short event.
/// </summary>
public sealed class ActivityLogTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 19, 10, 15, 0, TimeSpan.Zero);

    private readonly string folder = Path.Combine(Path.GetTempPath(), "atcsoft-activity-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_Second_Keeps_The_Most_Of_Its_Pictures_And_Is_Done_When_The_Next_One_Begins()
    {
        var accumulator = new ActivityAccumulator(Start);

        accumulator.Add(Start.AddSeconds(2.0), Result(1.5)).Should().BeNull();
        accumulator.Add(Start.AddSeconds(2.25), Result(4.0, 7)).Should().BeNull();
        accumulator.Add(Start.AddSeconds(2.5), Result(2.0, 9, 7)).Should().BeNull();
        var second = accumulator.Add(Start.AddSeconds(3.0), Result(0));

        second.Should().BeEquivalentTo(new ActivitySecond(2, 4.0, 2, [7, 9]));
        accumulator.Flush().Should().BeEquivalentTo(new ActivitySecond(3, 0, 0, []));
        accumulator.Flush().Should().BeNull();
    }

    [Fact]
    public void A_Clock_That_Steps_Back_Stays_In_The_Current_Second()
    {
        var accumulator = new ActivityAccumulator(Start);
        accumulator.Add(Start.AddSeconds(5.5), Result(1));

        accumulator.Add(Start.AddSeconds(4.9), Result(3)).Should().BeNull();
        accumulator.Add(Start.AddSeconds(-10), Result(2)).Should().BeNull();

        accumulator.Flush().Should().BeEquivalentTo(new ActivitySecond(5, 3, 0, []));
    }

    [Fact]
    public void What_Is_Written_Is_Read_Back()
    {
        var path = RecordingPathHelper.BuildActivityPath(Path.Combine(folder, "Cam", "Cam_20261019_101500_000.mkv"));
        using (var writer = ActivityLogWriter.Create(path, Start))
        {
            writer.Add(Start.AddSeconds(0.1), Result(0.04));
            writer.Add(Start.AddSeconds(1.2), Result(12.345, 3));
            writer.Add(Start.AddSeconds(4.0), Result(0));
        }

        var log = ActivityLogReader.Read(path);

        log.Should().NotBeNull();
        log!.Start.Should().Be(Start);
        log.Seconds.Should().BeEquivalentTo(
            [
                new ActivitySecond(0, 0, 0, []),
                new ActivitySecond(1, 12.3, 1, [3]),
                new ActivitySecond(4, 0, 0, []),
            ],
            o => o.WithStrictOrdering());
        log.DurationSeconds.Should().Be(5);
    }

    [Fact]
    public void The_Index_Can_Be_Read_While_It_Is_Written()
    {
        var path = Path.Combine(folder, "Cam_20261019_101500_000.activity.jsonl");
        using var writer = ActivityLogWriter.Create(path, Start);
        writer.Add(Start.AddSeconds(0.5), Result(2, 1));
        writer.Add(Start.AddSeconds(1.5), Result(3, 1));

        var log = ActivityLogReader.Read(path);

        log!.Seconds.Should().ContainSingle().Which.Offset.Should().Be(0, "the second in progress is not written yet");
    }

    [Fact]
    public void A_Line_Cut_Off_Is_Skipped_And_The_Rest_Is_Read()
    {
        var text = string.Join(
            '\n',
            """{"v":1,"start":"2026-10-19T10:15:00.000Z"}""",
            """{"t":0,"a":1.5,"n":0}""",
            """not json""",
            """{"t":2,"a":3,"n":1,"ids":[4]}""",
            """{"t":3,"a":""");

        var log = ActivityLogReader.Read(new StringReader(text));

        log!.Seconds.Select(s => s.Offset).Should().Equal(0, 2);
        log.Seconds[1].TrackIds.Should().Equal(4);
    }

    [Fact]
    public void The_Watch_Zones_Of_A_Second_Are_Kept_In_The_Order_Seen_And_Read_Back()
    {
        var path = Path.Combine(folder, "Cam_20261019_101500_000.activity.jsonl");
        using (var writer = ActivityLogWriter.Create(path, Start))
        {
            writer.Add(Start.AddSeconds(0.2), Zoned("Gate", 1));
            writer.Add(Start.AddSeconds(0.5), Zoned("Path", 2));
            writer.Add(Start.AddSeconds(0.7), Zoned("Gate", 1));
            writer.Add(Start.AddSeconds(1.2), Result(1, 1));
        }

        var log = ActivityLogReader.Read(path)!;

        log.Seconds[0].Zones.Should().Equal("Gate", "Path");
        log.Seconds[1].Zones.Should().BeEmpty("a box outside every watch zone has none");
        File.ReadAllLines(path)[2].Should().NotContain("\"z\"", "a second without zones leaves them out");
    }

    [Fact]
    public void A_Line_With_Fields_The_Reader_Does_Not_Know_Is_Still_Read()
    {
        // What a newer writer might add: the reader of version 1 skips it.
        var text = string.Join(
            '\n',
            """{"v":1,"start":"2026-10-19T10:15:00.000Z"}""",
            """{"t":2,"a":3,"n":1,"later":[{"x":[1,2]},"y"],"ids":[4],"also":7,"z":["Gate",5,"Path"]}""");

        var second = ActivityLogReader.Read(new StringReader(text))!.Seconds.Single();

        (second.Offset, second.Level, second.Tracks).Should().Be((2, 3.0, 1));
        second.TrackIds.Should().Equal(4);
        second.Zones.Should().Equal("Gate", "Path");
    }

    [Fact]
    public void A_Stretch_Keeps_The_Zones_Of_Its_Seconds()
    {
        var log = new ActivityLog(
            Start,
            [
                new ActivitySecond(0, 5, 1, [1]) { Zones = ["Gate"] },
                new ActivitySecond(1, 5, 1, [1]) { Zones = ["Path", "Gate"] },
                new ActivitySecond(2, 0, 0, []),
            ]);

        var buckets = ActivityDownsampler.ByMax(log, 4, 2);

        buckets[0].Zones.Should().Equal("Gate", "Path");
        buckets[1].Zones.Should().BeEmpty();
    }

    [Fact]
    public void Without_A_Header_Or_A_File_There_Is_No_Index()
    {
        ActivityLogReader.Read(new StringReader("""{"t":0,"a":1,"n":0}""")).Should().BeNull();
        ActivityLogReader.Read(new StringReader(string.Empty)).Should().BeNull();
        ActivityLogReader.Read(Path.Combine(folder, "missing.activity.jsonl")).Should().BeNull();
    }

    [Fact]
    public void A_Five_Second_Event_In_An_Hour_Shows_At_Its_Full_Height_On_A_Thousand_Buckets()
    {
        var seconds = Enumerable.Range(0, 3600)
            .Select(t => t is >= 1800 and < 1805
                ? new ActivitySecond(t, 40, 1, [1])
                : new ActivitySecond(t, 0.5, 0, []))
            .ToList();

        var buckets = ActivityDownsampler.ByMax(new ActivityLog(Start, seconds), 3600, 1000);

        buckets.Should().HaveCount(1000);
        buckets.Max(b => b.Level).Should().Be(40, "the most of a stretch is kept, never the average");
        buckets.Count(b => b.Tracks == 1).Should().BeInRange(2, 3);
        buckets.Should().OnlyContain(b => b.IsAnalysed);
    }

    [Fact]
    public void With_More_Buckets_Than_Seconds_A_Second_Fills_Every_Bucket_It_Covers()
    {
        var log = new ActivityLog(Start, [new ActivitySecond(1, 10, 1, [1])]);

        var buckets = ActivityDownsampler.ByMax(log, 4, 8);

        buckets.Select(b => b.Level).Should().Equal(0, 0, 10, 10, 0, 0, 0, 0);
        buckets.Select(b => b.IsAnalysed).Should().Equal(false, false, true, true, false, false, false, false);
    }

    [Fact]
    public void Seconds_Past_The_Recording_Are_Left_Out_And_No_Length_Means_The_Index_Length()
    {
        var log = new ActivityLog(Start, [new ActivitySecond(0, 1, 0, []), new ActivitySecond(9, 5, 0, [])]);

        ActivityDownsampler.ByMax(log, 5, 5).Select(b => b.Level).Should().Equal(1, 0, 0, 0, 0);
        ActivityDownsampler.ByMax(log, 0, 2).Select(b => b.Level).Should().Equal(1, 5);
        ActivityDownsampler.ByMax(new ActivityLog(Start, []), 0, 3).Should().OnlyContain(b => !b.IsAnalysed);
    }

    [Fact]
    public void The_Index_Sits_Beside_Its_Recording_And_Is_One_Of_Its_Companions()
    {
        var video = Path.Combine(folder, "Cam", "2026-10-19", "Cam_20261019_101500_000.mkv");

        var activity = RecordingPathHelper.BuildActivityPath(video);

        activity.Should().Be(Path.Combine(folder, "Cam", "2026-10-19", "Cam_20261019_101500_000.activity.jsonl"));
        RecordingPathHelper.GetCompanionPaths(video, folder).Should().Contain(activity)
            .And.Contain(RecordingPathHelper.GetThumbnailCandidates(video, folder));
    }

    private static MotionDetectorResult Zoned(
        string zone,
        int trackId)
        => new(true, MotionHoldChange.Continues, 2, [new BoundingBox { Width = 10, Height = 10, TrackId = trackId, Zone = zone }]);

    private static MotionDetectorResult Result(
        double changePercent,
        params int[] trackIds)
        => new(
            trackIds.Length > 0,
            trackIds.Length > 0 ? MotionHoldChange.Continues : MotionHoldChange.None,
            changePercent,
            [.. trackIds.Select(id => new BoundingBox { X = 0, Y = 0, Width = 10, Height = 10, TrackId = id })]);
}