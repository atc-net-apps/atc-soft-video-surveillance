namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// A camera's timeline over hours or days: built from its recordings' summaries, with coverage
/// from the video files, stretches that keep the most that happened, and events to the second
/// joined across segments.
/// </summary>
public sealed class TimelineTests : IDisposable
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    private readonly string folder = Path.Combine(Path.GetTempPath(), "atcsoft-timeline-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_Summary_Keeps_Ten_Second_Stretches_And_The_Events_To_The_Second()
    {
        var log = Log(Nine, (0, 1, 0), (3, 2, 0), (12, 30, 1), (13, 25, 2), (17, 5, 1), (40, 2, 1));

        var summary = ActivitySummarizer.Summarize(log);

        summary.DurationSeconds.Should().Be(41);
        summary.Buckets.Select(b => (b.Level, b.Tracks, b.IsAnalysed)).Should().Equal(
            (2, 0, true), (30, 2, true), (0, 0, false), (0, 0, false), (2, 1, true));
        summary.Events.Should().Equal(
            new ActivitySpan(12, 18, 30, 2),
            new ActivitySpan(40, 41, 2, 1));
    }

    [Fact]
    public void Recorded_But_Not_Analysed_Is_Not_A_Gap_And_Not_Quiet()
    {
        // Nine to ten in six ten-minute stretches: recorded from 9:00 to 9:20 with an index for
        // its first ten minutes only, nothing from 9:20 to 9:40, recorded from 9:40 without an index.
        var withIndex = new TimelineRecording("Cam/a.mkv", Nine, Nine.AddMinutes(20), ActivitySummarizer.Summarize(Log(Nine, [.. Enumerable.Range(0, 600).Select(s => (s, 1.0, 0))])));
        var withoutIndex = new TimelineRecording("Cam/b.mkv", Nine.AddMinutes(40), Nine.AddMinutes(70), Activity: null);

        var timeline = TimelineBuilder.Build([withoutIndex, withIndex], Nine, Nine.AddHours(1), 6);

        timeline.Stretches.Select(s => (s.IsRecorded, s.IsAnalysed)).Should().Equal(
            (true, true), (true, false), (false, false), (false, false), (true, false), (true, false));
        timeline.Coverage.Should().Equal(
            new CoverageSpan(Nine, Nine.AddMinutes(20), "Cam/a.mkv", Nine),
            new CoverageSpan(Nine.AddMinutes(40), Nine.AddHours(1), "Cam/b.mkv", Nine.AddMinutes(40)));
    }

    [Fact]
    public void A_Short_Event_In_A_Long_Stretch_Marks_It_At_Its_Full_Height()
    {
        var recording = new TimelineRecording("Cam/a.mkv", Nine, Nine.AddHours(1), ActivitySummarizer.Summarize(Log(Nine, (1800, 40, 1), (1801, 35, 1))));

        var timeline = TimelineBuilder.Build([recording], Nine, Nine.AddHours(1), 4);

        timeline.Stretches.Select(s => s.Level).Should().Equal(0, 0, 40, 0);
        timeline.Stretches.Select(s => s.Tracks).Should().Equal(0, 0, 1, 0);
        timeline.Events.Should().ContainSingle().Which.Should().Be(
            new TimelineActivityEvent(Nine.AddSeconds(1800), Nine.AddSeconds(1802), 40, 1, "Cam/a.mkv", 1800));
    }

    [Fact]
    public void An_Event_Across_Two_Segments_Is_One_Event_Played_From_The_First()
    {
        var first = new TimelineRecording("Cam/a.mkv", Nine, Nine.AddSeconds(60), ActivitySummarizer.Summarize(Log(Nine, (57, 10, 1), (58, 10, 1), (59, 12, 2))));
        var second = new TimelineRecording("Cam/b.mkv", Nine.AddSeconds(61), Nine.AddSeconds(120), ActivitySummarizer.Summarize(Log(Nine.AddSeconds(61), (0, 20, 1), (1, 8, 1))));

        var events = TimelineBuilder.FindEvents([second, first]);

        events.Should().ContainSingle().Which.Should().Be(
            new TimelineActivityEvent(Nine.AddSeconds(57), Nine.AddSeconds(63), 20, 2, "Cam/a.mkv", 57));
    }

    [Fact]
    public void Only_Events_Touching_The_Span_Are_In_It()
    {
        var recording = new TimelineRecording("Cam/a.mkv", Nine, Nine.AddHours(1), ActivitySummarizer.Summarize(Log(Nine, (60, 5, 1), (1800, 5, 1), (3500, 5, 1))));

        var timeline = TimelineBuilder.Build([recording], Nine.AddMinutes(20), Nine.AddMinutes(40), 10);

        timeline.Events.Should().ContainSingle().Which.Start.Should().Be(Nine.AddSeconds(1800));
        timeline.Coverage.Should().ContainSingle().Which.Should().Be(new CoverageSpan(Nine.AddMinutes(20), Nine.AddMinutes(40), "Cam/a.mkv", Nine), "the cut keeps where the recording starts");
    }

    [Fact]
    public void A_Span_That_Does_Not_End_After_It_Starts_Is_Refused()
    {
        var build = () => TimelineBuilder.Build([], Nine, Nine, 10);

        build.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void The_Cache_Reads_An_Index_Once_Again_When_It_Grows_And_Keeps_No_More_Than_Its_Capacity()
    {
        var cache = new ActivitySummaryCache(capacity: 2);
        var a = WriteIndex("a", 3);
        var b = WriteIndex("b", 3);
        var c = WriteIndex("c", 3);

        cache.Get(a)!.DurationSeconds.Should().Be(3);
        File.AppendAllText(a, """{"t":9,"a":1,"n":0}""" + "\n");
        cache.Get(a)!.DurationSeconds.Should().Be(10, "a grown index is read again");

        cache.Get(b);
        cache.Get(c);
        cache.Count.Should().Be(2);
        cache.Get(Path.Combine(folder, "missing.activity.jsonl")).Should().BeNull();
    }

    [Fact]
    public void A_Week_Of_Hour_Long_Indexes_For_Ten_Cameras_Is_Read_Fast_Enough_For_A_Timeline()
    {
        // A cold timeline request summarizes every index in its span once. Here: 24 hour-long
        // indexes, scaled to 7 days x 10 cameras (1 680 files).
        var files = Enumerable.Range(0, 24).Select(i => WriteIndex($"hour{i}", 3600)).ToList();
        var cache = new ActivitySummaryCache();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (var file in files)
        {
            cache.Get(file).Should().NotBeNull();
        }

        watch.Stop();
        var perFile = watch.Elapsed.TotalMilliseconds / files.Count;
        var week = TimeSpan.FromMilliseconds(perFile * 7 * 24 * 10);
        TestContext.Current.TestOutputHelper?.WriteLine($"One hour-long index: {perFile:N1} ms; a cold week of ten cameras: {week.TotalSeconds:N1} s.");
        week.Should().BeLessThan(TimeSpan.FromSeconds(20), $"one hour-long index takes {perFile:N1} ms");
    }

    private static ActivityLog Log(
        DateTimeOffset start,
        params (int Offset, double Level, int Tracks)[] seconds)
        => new(start, [.. seconds.Select(s => new ActivitySecond(s.Offset, s.Level, s.Tracks, [.. Enumerable.Range(1, s.Tracks)]))]);

    private string WriteIndex(
        string name,
        int seconds)
    {
        var path = Path.Combine(folder, name + ".activity.jsonl");
        using var writer = ActivityLogWriter.Create(path, Nine);
        for (var s = 0; s < seconds; s++)
        {
            var moving = s % 97 < 5;
            writer.Add(
                Nine.AddSeconds(s),
                new MotionDetectorResult(moving, MotionHoldChange.None, moving ? 12 : 1.5, moving ? [new BoundingBox { Width = 5, Height = 5, TrackId = s / 97 }] : []));
        }

        return path;
    }
}