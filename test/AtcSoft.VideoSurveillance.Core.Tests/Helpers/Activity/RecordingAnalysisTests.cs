namespace AtcSoft.VideoSurveillance.Helpers.Activity;

using BuiltInMotionDetector = AtcSoft.VideoSurveillance.Helpers.MotionDetection.BuiltInMotionDetector;

/// <summary>
/// *Analyse recording*: an older recording gets the same activity index as one analysed while it
/// was made, never half of one.
/// </summary>
public sealed class RecordingAnalysisTests : IDisposable
{
    private const int Width = 160;
    private const int Height = 120;
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    private readonly string folder = Path.Combine(Path.GetTempPath(), "atcsoft-analysis-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_Recording_Gets_An_Index_With_Its_Event_In_Place()
    {
        var video = Video();
        var start = RecordingAnalysis.StartOf(video, Copenhagen);

        var analysed = RecordingAnalysis.Run(Pictures(), Width, Height, new BuiltInMotionDetector(new MotionDetectionSettings()), video, start, cancellationToken: TestContext.Current.CancellationToken);

        analysed.Should().Be(80);
        start.Should().Be(new DateTimeOffset(2026, 10, 19, 7, 0, 0, TimeSpan.Zero), "the name's 09:00 is Copenhagen time");
        var log = ActivityLogReader.Read(RecordingPathHelper.BuildActivityPath(video))!;
        log.Start.Should().Be(start);
        log.Seconds.Should().HaveCount(20, "every second was analysed");
        log.Seconds.Where(s => s.Tracks > 0).Select(s => s.Offset).Should().NotBeEmpty().And.OnlyContain(offset => offset >= 5 && offset <= 15, "the square moves from 5 to 15 s");
        File.Exists(RecordingPathHelper.BuildActivityPath(video) + ".part").Should().BeFalse();
    }

    [Fact]
    public void A_Cancelled_Analysis_Leaves_No_Index_Behind()
    {
        var video = Video();
        using var cancellation = new CancellationTokenSource();

        var run = () => RecordingAnalysis.Run(
            Pictures().Select((p, i) =>
            {
                if (i == 10)
                {
                    cancellation.Cancel();
                }

                return p;
            }),
            Width,
            Height,
            new BuiltInMotionDetector(new MotionDetectionSettings()),
            video,
            DateTimeOffset.UnixEpoch,
            cancellation.Token);

        run.Should().Throw<OperationCanceledException>();
        Directory.GetFiles(Path.GetDirectoryName(video)!).Should().Equal(video);
    }

    [Fact]
    public void A_Cameras_Ignore_Zone_Over_The_Path_Leaves_No_Event()
    {
        // A band across the middle of the picture, where the square crosses (rows 50 to 70 of 120).
        var camera = new CameraConfiguration
        {
            MotionZones = [new MotionZone { Name = "Road", Points = [new(0, 0.35), new(1, 0.35), new(1, 0.65), new(0, 0.65)] }],
        };
        var video = Video();

        RecordingAnalysis.Run(
            Pictures(),
            Width,
            Height,
            RecordingAnalysis.DetectorFor(camera, new MotionDetectionSettings(), recordingRotation: 0),
            video,
            DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken);

        var log = ActivityLogReader.Read(RecordingPathHelper.BuildActivityPath(video))!;
        log.Seconds.Should().HaveCount(20);
        log.Seconds.Should().OnlyContain(s => s.Tracks == 0, "the square moves only inside the zone");
    }

    [Fact]
    public void A_Track_Through_A_Watch_Zone_Leaves_The_Zone_In_The_Index()
    {
        // The square crosses rows 50 to 70 of 120 from the left: a watch zone on the right half.
        var camera = new CameraConfiguration
        {
            MotionZones = [new MotionZone { Name = "Gate", Kind = MotionZoneKind.Watch, Points = [new(0.5, 0), new(1, 0), new(1, 1), new(0.5, 1)] }],
        };
        var video = Video();

        RecordingAnalysis.Run(
            Pictures(),
            Width,
            Height,
            RecordingAnalysis.DetectorFor(camera, new MotionDetectionSettings(), recordingRotation: 0),
            video,
            DateTimeOffset.UnixEpoch,
            TestContext.Current.CancellationToken);

        var moving = ActivityLogReader.Read(RecordingPathHelper.BuildActivityPath(video))!.Seconds.Where(s => s.Tracks > 0).ToList();
        moving.Should().Contain(s => s.Zones.SequenceEqual(new[] { "Gate" }), "the square crosses the zone");
        moving.Should().Contain(s => s.Zones.Count == 0, "it is outside the zone at first");
    }

    [Fact]
    public void The_First_Seconds_Of_A_Recording_Only_Teach_The_Scene()
    {
        // The square already moves when the recording starts.
        var warmed = Video();
        var cold = Path.Combine(folder, "Garden", "Garden_20261019_090100_000.mkv");
        File.WriteAllText(cold, "video");

        RecordingAnalysis.Run(Pictures(from: 0, to: 10), Width, Height, RecordingAnalysis.DetectorFor(null, new MotionDetectionSettings(), recordingRotation: 0), warmed, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);
        RecordingAnalysis.Run(Pictures(from: 0, to: 10), Width, Height, new BuiltInMotionDetector(new MotionDetectionSettings()), cold, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

        var moving = ActivityLogReader.Read(RecordingPathHelper.BuildActivityPath(warmed))!.Seconds.Where(s => s.Tracks > 0).Select(s => s.Offset).ToList();
        moving.Should().NotBeEmpty().And.OnlyContain(offset => offset >= RecordingAnalysis.WarmUpSeconds, "no box while the scene is learnt");
        moving.Should().Contain(offset => offset < 5, "the square is found soon after");
        ActivityLogReader.Read(RecordingPathHelper.BuildActivityPath(cold))!.Seconds.Where(s => s.Tracks > 0).Min(s => s.Offset)
            .Should().BeLessThan((int)RecordingAnalysis.WarmUpSeconds, "a detector without the warm-up, as a live camera's, boxes it at once");
    }

    [Theory]
    [InlineData(90, 0.1, 0.2)]
    [InlineData(0, 0.2, 0.9)]
    [InlineData(-270, 0.1, 0.2)]
    public void The_Zones_Are_Turned_From_The_Cameras_Rotation_To_The_Recordings(
        int recordingRotation,
        double x,
        double y)
    {
        // Drawn on the picture turned a quarter; a recording made before the turn is not turned.
        var camera = new CameraConfiguration
        {
            MotionZones = [new MotionZone { Name = "Tree", Points = [new(0.1, 0.2), new(0.5, 0.2), new(0.5, 0.6)] }],
        };
        camera.Display.Rotation = CameraRotation.Rotate90;

        var zones = RecordingAnalysis.ZonesFor(camera, recordingRotation);

        zones.Single().Points[0].X.Should().BeApproximately(x, 1e-9);
        zones.Single().Points[0].Y.Should().BeApproximately(y, 1e-9);
        camera.MotionZones[0].Points[0].Should().Be(new ZonePoint(0.1, 0.2), "the camera's zones are left as they are");
    }

    [Fact]
    public void A_Recording_Of_A_Camera_Not_Known_Is_Analysed_With_The_Apps_Settings_And_No_Zones()
    {
        var appSettings = new MotionDetectionSettings();

        RecordingAnalysis.SettingsFor(null, appSettings).Should().BeSameAs(appSettings);
        RecordingAnalysis.ZonesFor(null, 90).Should().BeEmpty();
        RecordingAnalysis.SettingsFor(new CameraConfiguration(), appSettings).Should().NotBeSameAs(appSettings, "a camera's own settings are its overrides on a copy");
    }

    // Twenty seconds at four pictures a second: a grey yard, and a dark square crossing it from 5 to 15 s.
    private static IEnumerable<(TimeSpan Position, byte[] Pixels)> Pictures(
        double from = 5,
        double to = 15)
    {
        for (var i = 0; i < 80; i++)
        {
            var pixels = Enumerable.Repeat((byte)128, Width * Height).ToArray();
            var seconds = i / 4.0;
            if (seconds >= from && seconds < to)
            {
                var left = (int)((seconds - from) * 12);
                for (var y = 50; y < 70; y++)
                {
                    for (var x = left; x < left + 20; x++)
                    {
                        pixels[(y * Width) + x] = 20;
                    }
                }
            }

            yield return (TimeSpan.FromSeconds(seconds), pixels);
        }
    }

    private string Video()
    {
        var video = Path.Combine(folder, "Garden", "Garden_20261019_090000_000.mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(video)!);
        File.WriteAllText(video, "video");
        return video;
    }
}