namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Every recording gets its activity index beside it, written while the recording is made, a
/// new one per segment, and the camera is analysed for it only while it records. The same in
/// CameraWall and on the server.
/// </summary>
public sealed class ActivityIndexRecorderTests : IDisposable
{
    private static readonly MotionDetectionSettings GardenSettings = new() { Sensitivity = 70 };

    private readonly Guid camera = Guid.NewGuid();
    private readonly string folder = Path.Combine(Path.GetTempPath(), "atcsoft-activity-recorder-" + Guid.NewGuid().ToString("N"));
    private readonly IMediaPipeline pipeline = NullPipeline.New();
    private readonly FakeActiveRecordings recordings = new();
    private readonly FakeMotionDetection motion = new();
    private readonly ActivityIndexRecorder sut;

    public ActivityIndexRecorderTests()
    {
        sut = new ActivityIndexRecorder(
            NullLogger.Instance,
            recordings,
            motion,
            id => id == camera ? GardenSettings : null);
        sut.Start();
    }

    public void Dispose()
    {
        sut.Dispose();
        pipeline.Dispose();
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_Recording_Gets_An_Index_Beside_It_And_Its_Camera_Is_Analysed_For_It()
    {
        var video = Video("101500");

        recordings.Start(camera, video, pipeline);

        sut.GetIndexPath(camera).Should().Be(RecordingPathHelper.BuildActivityPath(video));
        File.Exists(RecordingPathHelper.BuildActivityPath(video)).Should().BeTrue();
        motion.Indexing.Should().ContainKey(camera).WhoseValue.Should().BeSameAs(GardenSettings);
    }

    [Fact]
    public void Every_Analysed_Picture_Goes_Into_The_Index()
    {
        var video = Video("101500");
        recordings.Start(camera, video, pipeline);
        var start = recordings.GetSession(camera)!.StartTime;

        motion.Picture(camera, Utc(start).AddSeconds(0.2), 1);
        motion.Picture(camera, Utc(start).AddSeconds(1.2), 30);
        motion.Picture(Guid.NewGuid(), Utc(start).AddSeconds(1.5), 99);
        recordings.Stop(camera);

        var log = ActivityLogReader.Read(RecordingPathHelper.BuildActivityPath(video));
        log!.Seconds.Select(s => (s.Offset, s.Level)).Should().Equal((0, 1), (1, 30));
    }

    [Fact]
    public void A_New_Segment_Gets_An_Index_Of_Its_Own_And_The_Camera_Stays_Analysed()
    {
        var first = Video("101500");
        var second = Video("103000");
        recordings.Start(camera, first, pipeline);
        motion.Picture(camera, DateTimeOffset.UtcNow, 5);

        recordings.Segment(camera, second);
        motion.Picture(camera, DateTimeOffset.UtcNow.AddSeconds(2), 7);

        ActivityLogReader.Read(RecordingPathHelper.BuildActivityPath(first))!.Seconds.Should().ContainSingle();
        sut.GetIndexPath(camera).Should().Be(RecordingPathHelper.BuildActivityPath(second));
        motion.IndexingStopped.Should().BeEmpty();
    }

    [Fact]
    public void A_Change_Of_State_Within_The_Same_File_Keeps_What_Was_Written()
    {
        // CameraWall reports motion and post-motion as states of the same recording.
        var video = Video("101500");
        recordings.Start(camera, video, pipeline);
        var start = Utc(recordings.GetSession(camera)!.StartTime);
        motion.Picture(camera, start, 5);
        motion.Picture(camera, start.AddSeconds(1), 6);

        recordings.ChangeState(camera, RecordingState.Recording, RecordingState.RecordingMotion, video);
        recordings.Stop(camera);

        ActivityLogReader.Read(RecordingPathHelper.BuildActivityPath(video))!.Seconds.Should().HaveCount(2);
    }

    [Fact]
    public void When_The_Recording_Stops_The_Index_Is_Closed_And_The_Camera_Is_No_Longer_Analysed_For_It()
    {
        recordings.Start(camera, Video("101500"), pipeline);

        recordings.Stop(camera);
        motion.Picture(camera, DateTimeOffset.UtcNow, 50);

        sut.GetIndexPath(camera).Should().BeNull();
        motion.IndexingStopped.Should().Equal(camera);
    }

    [Fact]
    public void Activity_That_Starts_Is_Told_And_Ends_With_The_Recording()
    {
        var changes = new List<CameraActivityChangedEventArgs>();
        sut.ActivityChanged += (_, e) => changes.Add(e);
        var video = Video("101500");
        recordings.Start(camera, video, pipeline);

        motion.MovingPicture(camera, DateTimeOffset.UtcNow);
        recordings.Stop(camera);

        changes.Select(c => c.IsActive).Should().Equal(true, false);
        changes.Should().OnlyContain(c => c.CameraId == camera && c.VideoPath == video);
    }

    [Fact]
    public void A_Camera_That_Is_Not_Known_Gets_An_Index_But_Is_Not_Analysed()
    {
        var stranger = Guid.NewGuid();

        recordings.Start(stranger, Video("101500"), pipeline);

        sut.GetIndexPath(stranger).Should().NotBeNull();
        motion.Indexing.Should().BeEmpty();
    }

    [Fact]
    public void A_Recording_That_Was_Running_Before_The_Start_Gets_Its_Index()
    {
        using var late = new ActivityIndexRecorder(NullLogger.Instance, recordings, motion, _ => GardenSettings);
        var other = Guid.NewGuid();
        var video = Video("111500");
        recordings.Start(other, video, pipeline, raise: false);

        late.Start();

        late.GetIndexPath(other).Should().Be(RecordingPathHelper.BuildActivityPath(video));
    }

    [Fact]
    public void Turned_Off_A_Recording_Gets_No_Index_And_No_Analysis_And_Turned_On_The_Next_Segment_Does()
    {
        var ownRecordings = new FakeActiveRecordings();
        var ownMotion = new FakeMotionDetection();
        var enabled = false;
        using var recorder = new ActivityIndexRecorder(NullLogger.Instance, ownRecordings, ownMotion, _ => GardenSettings, () => enabled);
        recorder.Start();
        var first = Video("121500");
        var second = Video("123000");

        ownRecordings.Start(camera, first, pipeline);

        recorder.GetIndexPath(camera).Should().BeNull();
        File.Exists(RecordingPathHelper.BuildActivityPath(first)).Should().BeFalse();
        ownMotion.Indexing.Should().BeEmpty();

        enabled = true;
        ownRecordings.Segment(camera, second);

        recorder.GetIndexPath(camera).Should().Be(RecordingPathHelper.BuildActivityPath(second));
        ownMotion.Indexing.Should().ContainKey(camera);
    }

    private static DateTimeOffset Utc(DateTime time)
        => new(DateTime.SpecifyKind(time.ToUniversalTime(), DateTimeKind.Utc));

    private string Video(string time)
        => Path.Combine(folder, "Garden", $"Garden_20261019_{time}_000.mkv");
}