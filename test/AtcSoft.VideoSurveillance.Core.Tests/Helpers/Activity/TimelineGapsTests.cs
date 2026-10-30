namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// A stretch of a camera's timeline without a recording is explained from the history of whether
/// the camera could be recorded: offline, disk full, server not running, or not recording.
/// </summary>
public sealed class TimelineGapsTests : IDisposable
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    private readonly string folder = Path.Combine(Path.GetTempPath(), "atcsoft-availability-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Each_Gap_Takes_The_State_At_Its_Start()
    {
        // Recorded 9:00-9:10, 9:20-9:30, 9:40-9:50; in between: offline, then the disk filled up.
        CoverageSpan[] coverage =
        [
            new(Nine, Nine.AddMinutes(10), "a", Nine),
            new(Nine.AddMinutes(20), Nine.AddMinutes(30), "b", Nine.AddMinutes(20)),
            new(Nine.AddMinutes(40), Nine.AddMinutes(50), "c", Nine.AddMinutes(40)),
        ];
        AvailabilityEntry[] history =
        [
            new(Nine.AddMinutes(-5), AvailabilityChange.ServerStarted),
            new(Nine, AvailabilityChange.RecordingStarted),
            new(Nine.AddMinutes(10), AvailabilityChange.Offline),
            new(Nine.AddMinutes(20), AvailabilityChange.Online),
            new(Nine.AddMinutes(20), AvailabilityChange.RecordingStarted),
            new(Nine.AddMinutes(30), AvailabilityChange.DiskFull),
            new(Nine.AddMinutes(40), AvailabilityChange.RecordingStarted),
            new(Nine.AddMinutes(50), AvailabilityChange.RecordingStopped),
        ];

        var gaps = TimelineGaps.Find(coverage, Nine, Nine.AddHours(1), history);

        gaps.Should().Equal(
            new TimelineGap(Nine.AddMinutes(10), Nine.AddMinutes(20), GapReason.Offline),
            new TimelineGap(Nine.AddMinutes(30), Nine.AddMinutes(40), GapReason.DiskFull),
            new TimelineGap(Nine.AddMinutes(50), Nine.AddHours(1), GapReason.NotRecording));
    }

    [Fact]
    public void Without_History_A_Gap_Is_Unknown_And_A_Stopped_Server_Explains_Its_Own()
    {
        CoverageSpan[] coverage = [new(Nine.AddMinutes(30), Nine.AddMinutes(40), "a", Nine.AddMinutes(30))];
        AvailabilityEntry[] history =
        [
            new(Nine.AddMinutes(30), AvailabilityChange.ServerStarted),
            new(Nine.AddMinutes(40), AvailabilityChange.ServerStopped),
        ];

        TimelineGaps.Find(coverage, Nine, Nine.AddHours(1), history).Select(g => g.Reason)
            .Should().Equal(GapReason.Unknown, GapReason.ServerDown);
    }

    [Fact]
    public void A_Gap_Is_Cut_Where_Its_Reason_Changes()
    {
        CoverageSpan[] coverage = [new(Nine, Nine.AddMinutes(10), "a", Nine)];
        AvailabilityEntry[] history =
        [
            new(Nine, AvailabilityChange.RecordingStarted),
            new(Nine.AddMinutes(10), AvailabilityChange.Offline),
            new(Nine.AddMinutes(20), AvailabilityChange.Offline),
            new(Nine.AddMinutes(30), AvailabilityChange.Online),
            new(Nine.AddMinutes(30), AvailabilityChange.DiskFull),
        ];

        TimelineGaps.Find(coverage, Nine, Nine.AddHours(1), history).Should().Equal(
            new TimelineGap(Nine.AddMinutes(10), Nine.AddMinutes(30), GapReason.Offline),
            new TimelineGap(Nine.AddMinutes(30), Nine.AddHours(1), GapReason.DiskFull));
    }

    [Fact]
    public void A_Camera_That_Went_On_And_Off_Every_Minute_For_A_Week_Is_Read_In_One_Pass()
    {
        // A recording every other minute for a week, offline in between.
        var start = Nine.AddDays(-7);
        var coverage = new List<CoverageSpan>();
        var history = new List<AvailabilityEntry>();
        for (var minute = 0; minute < 7 * 24 * 60; minute += 2)
        {
            coverage.Add(new CoverageSpan(start.AddMinutes(minute), start.AddMinutes(minute + 1), "a", start.AddMinutes(minute)));
            history.Add(new AvailabilityEntry(start.AddMinutes(minute), AvailabilityChange.Online));
            history.Add(new AvailabilityEntry(start.AddMinutes(minute + 1), AvailabilityChange.Offline));
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var gaps = TimelineGaps.Find(coverage, start, Nine, history);

        gaps.Should().HaveCount(coverage.Count).And.OnlyContain(g => g.Reason == GapReason.Offline);
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void A_Span_With_No_Recording_Is_One_Gap()
        => TimelineGaps.Find([], Nine, Nine.AddHours(1), []).Should().Equal(new TimelineGap(Nine, Nine.AddHours(1), GapReason.Unknown));

    [Fact]
    public void The_History_Is_Written_And_Read_Back_Skipping_A_Broken_Line()
    {
        var path = Path.Combine(folder, "availability", "camera.jsonl");
        AvailabilityLog.Append(path, new AvailabilityEntry(Nine.AddMinutes(1), AvailabilityChange.Offline));
        AvailabilityLog.Append(path, new AvailabilityEntry(Nine, AvailabilityChange.Online));
        File.AppendAllText(path, "{\"t\":\"2026-10-19T09:02:00.000Z\",\"c\":\"somethingNew\"}\n{\"t\":");

        AvailabilityLog.Read(path).Should().Equal(
            new AvailabilityEntry(Nine, AvailabilityChange.Online),
            new AvailabilityEntry(Nine.AddMinutes(1), AvailabilityChange.Offline));
        AvailabilityLog.Read(Path.Combine(folder, "missing.jsonl")).Should().BeEmpty();
    }
}