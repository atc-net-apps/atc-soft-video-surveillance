namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// CameraWall's timeline from its own folders: names read in the zone they were written in, the
/// index lined up with them, a camera's own folder inside the global one, recordings in progress.
/// </summary>
public sealed class LocalTimelineTests : IDisposable
{
    private static readonly DateTimeOffset Seven = new(2026, 10, 19, 7, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-local-timeline-" + Guid.NewGuid().ToString("N"));
    private readonly CameraConfiguration garden = Camera("Garden");
    private readonly CameraConfiguration porch = Camera("Porch");

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void A_Name_In_Local_Time_Is_Placed_At_Its_Instant_And_Its_Activity_With_It()
    {
        // CameraWall in Copenhagen (UTC+2 in October) names a recording started at 07:00Z "09:00".
        var video = Record(Path.Combine(root, "Garden", "2026-10-19", "Garden_20261019_090000_000.mkv"), Seven, TimeSpan.FromMinutes(10), eventAt: 300);

        var recordings = Find([root], [], Seven.AddHours(-1), Seven.AddHours(1));

        var recording = recordings[garden.Id].Should().ContainSingle().Subject;
        recording.VideoPath.Should().Be(video);
        recording.Start.Should().Be(Seven);
        recording.Activity!.Start.Should().Be(Seven);

        var timeline = TimelineBuilder.Build(recordings[garden.Id], Seven, Seven.AddMinutes(10), 10);
        timeline.Coverage.Should().ContainSingle().Which.Start.Should().Be(Seven);
        timeline.Events.Should().ContainSingle().Which.Start.Should().Be(Seven.AddSeconds(300));
        timeline.Events[0].OffsetSeconds.Should().Be(300);
    }

    [Fact]
    public void A_Cameras_Own_Folder_Inside_The_Global_One_Belongs_To_It_And_Thumbnails_And_Unknown_Folders_Are_Left_Out()
    {
        var own = Path.Combine(root, "porch-own");
        porch.Overrides.Recording.RecordingPath = own;
        Record(Path.Combine(own, "Porch", "Porch_20261019_090000_000.mkv"), Seven, TimeSpan.FromMinutes(1));
        Record(Path.Combine(root, RecordingPathHelper.ThumbnailsFolderName, "Garden", "Garden_20261019_090000_000.mkv"), Seven, TimeSpan.FromMinutes(1));
        Record(Path.Combine(root, "Renamed", "Renamed_20261019_090000_000.mkv"), Seven, TimeSpan.FromMinutes(1));

        var recordings = Find([root, own], [], Seven.AddHours(-1), Seven.AddHours(1));

        recordings.Keys.Should().Equal(porch.Id);
    }

    [Fact]
    public void A_Recording_In_Progress_Reaches_Now_And_One_Outside_The_Span_Is_Left_Out()
    {
        var current = Record(Path.Combine(root, "Garden", "Garden_20261019_095000_000.mkv"), Seven.AddMinutes(50), TimeSpan.FromMinutes(1));
        Record(Path.Combine(root, "Garden", "Garden_20261018_090000_000.mkv"), Seven.AddDays(-1), TimeSpan.FromMinutes(1));
        var session = new RecordingSession(garden.Id, "Garden", current, isManualRecording: true);
        var now = DateTimeOffset.UtcNow;

        var recordings = Find([root], [session], now.AddHours(-1), now.AddMinutes(1), now);

        var recording = recordings[garden.Id].Should().ContainSingle().Subject;
        recording.VideoPath.Should().Be(current);
        recording.End.Should().Be(now);
        recording.Start.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), "the session started as the test ran, an instant in any zone");
    }

    [Fact]
    public void Previous_And_Next_Event_Look_Across_Cameras()
    {
        Record(Path.Combine(root, "Garden", "Garden_20261019_090000_000.mkv"), Seven, TimeSpan.FromMinutes(10), eventAt: 300);
        Record(Path.Combine(root, "Porch", "Porch_20261019_090000_000.mkv"), Seven, TimeSpan.FromMinutes(10), eventAt: 120);
        var recordings = Find([root], [], Seven.AddHours(-1), Seven.AddHours(1));

        LocalTimeline.FindEvent(recordings, Seven, forward: true)!.Value.CameraId.Should().Be(porch.Id);
        LocalTimeline.FindEvent(recordings, Seven.AddMinutes(3), forward: true)!.Value.CameraId.Should().Be(garden.Id);
        LocalTimeline.FindEvent(recordings, Seven.AddMinutes(3), forward: false)!.Value.Event.Start.Should().Be(Seven.AddSeconds(120));
        LocalTimeline.FindEvent(recordings, Seven.AddMinutes(10), forward: true).Should().BeNull();
    }

    private static CameraConfiguration Camera(string name)
    {
        var camera = new CameraConfiguration();
        camera.Display.DisplayName = name;
        return camera;
    }

    private IReadOnlyDictionary<Guid, IReadOnlyList<TimelineRecording>> Find(
        IReadOnlyList<string?> folders,
        IReadOnlyList<RecordingSession> sessions,
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset? now = null)
        => LocalTimeline.FindRecordings(
            folders,
            [new TimelineCamera(garden.Id, "Garden"), new TimelineCamera(porch.Id, "Porch")],
            sessions,
            from,
            to,
            Copenhagen,
            now ?? to,
            new ActivitySummaryCache());

    private static string Record(
        string video,
        DateTimeOffset start,
        TimeSpan length,
        int? eventAt = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(video)!);
        File.WriteAllText(video, "video");
        File.SetLastWriteTimeUtc(video, (start + length).UtcDateTime);
        if (eventAt is not { } at)
        {
            return video;
        }

        using var index = ActivityLogWriter.Create(RecordingPathHelper.BuildActivityPath(video), start);
        for (var second = 0; second < (int)length.TotalSeconds; second++)
        {
            var moving = second >= at && second < at + 3;
            index.Add(
                start.AddSeconds(second),
                new MotionDetectorResult(moving, MotionHoldChange.None, moving ? 25 : 1, moving ? [new BoundingBox { Width = 5, Height = 5, TrackId = 1 }] : []));
        }

        return video;
    }
}