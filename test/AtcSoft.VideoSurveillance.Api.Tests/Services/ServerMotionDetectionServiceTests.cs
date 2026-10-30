namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// The server's motion detection, driven one picture at a time. The pictures
/// are a single byte each that the reader turns into a grayscale picture:
/// level 50 is the empty scene, anything else has a bright block in it.
/// These tests are about the service (events, the post-motion hold, failed
/// captures, stopping), so they use the simple frame-difference detector; the
/// detection itself is measured by the storybook tests.
/// </summary>
public sealed class ServerMotionDetectionServiceTests : IDisposable
{
    private const int Width = 320;
    private const int Height = 240;
    private const byte EmptyScene = 50;
    private const byte SceneWithBlock = 200;
    private const byte Unreadable = 0;

    private readonly Guid cameraId = Guid.NewGuid();
    private readonly IMediaPipeline pipeline = Substitute.For<IMediaPipeline>();
    private readonly FakeClock clock = new();
    private readonly List<MotionDetectedEventArgs> events = [];
    private readonly MotionDetectionSettings settings = new()
    {
        AnalysisWidth = Width,
        AnalysisHeight = Height,
        MinimumChangePercent = 0.5,
        PostMotionDurationSeconds = 10,
    };

    private readonly ServerMotionDetectionService sut;

    public ServerMotionDetectionServiceTests()
    {
        sut = new ServerMotionDetectionService(
            NullLogger<ServerMotionDetectionService>.Instance,
            new BlockReader(),
            clock,
            runLoops: false,
            createDetector: s => new FrameDifferenceMotionDetector(s));
        sut.MotionDetected += (_, e) => events.Add(e);
        sut.StartDetection(cameraId, pipeline, settings);
    }

    public void Dispose()
    {
        sut.Dispose();
        pipeline.Dispose();
    }

    [Fact]
    public async Task A_Scene_That_Does_Not_Change_Raises_Nothing()
    {
        await ShowAsync(EmptyScene, EmptyScene, EmptyScene);

        events.Should().BeEmpty();
        sut.IsMotionDetected(cameraId).Should().BeFalse();
    }

    [Fact]
    public async Task Something_Moving_Into_The_Scene_Is_Reported_With_Where_It_Is()
    {
        await ShowAsync(EmptyScene, SceneWithBlock);

        var motion = events.Should().ContainSingle().Subject;
        motion.CameraId.Should().Be(cameraId);
        motion.IsMotionActive.Should().BeTrue();
        motion.ChangePercentage.Should().BeGreaterThan(5);
        motion.BoundingBoxes.Should().ContainSingle();
        motion.AnalysisWidth.Should().Be(Width);
        motion.AnalysisHeight.Should().Be(Height);
        sut.IsMotionDetected(cameraId).Should().BeTrue();
        sut.GetLastBoundingBoxes(cameraId).Should().ContainSingle();
        sut.GetAnalysisResolution(cameraId).Should().Be((Width, Height));
    }

    [Fact]
    public async Task A_Grey_Picture_From_The_Pipeline_Is_Analysed_Without_A_Png()
    {
        // No PNG at all: asking for one would fail the analysis.
        pipeline.CaptureFrameAsync(Arg.Any<CancellationToken>()).Returns<byte[]?>(_ => throw new InvalidOperationException("no PNG wanted"));
        var reader = new BlockReader();
        foreach (var scene in new[] { EmptyScene, SceneWithBlock })
        {
            pipeline.CaptureGreyAsync(Width, Height, Arg.Any<CancellationToken>()).Returns(reader.ReadGrayscale([scene], Width, Height));
            await sut.AnalyzeOnceAsync(cameraId, TestContext.Current.CancellationToken);
        }

        events.Should().ContainSingle().Which.IsMotionActive.Should().BeTrue();
        await pipeline.DidNotReceive().CaptureFrameAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_Wide_Camera_Is_Analysed_In_Its_Own_Shape()
    {
        // 16:9 as displayed: the longer side as set, the height following, in the events too.
        pipeline.DisplaySize.Returns((1920, 1080));
        var reader = new BlockReader();
        foreach (var scene in new[] { EmptyScene, SceneWithBlock })
        {
            pipeline.CaptureGreyAsync(Width, 180, Arg.Any<CancellationToken>()).Returns(reader.ReadGrayscale([scene], Width, 180));
            await sut.AnalyzeOnceAsync(cameraId, TestContext.Current.CancellationToken);
        }

        var motion = events.Should().ContainSingle().Subject;
        (motion.AnalysisWidth, motion.AnalysisHeight).Should().Be((Width, 180));
        sut.GetAnalysisResolution(cameraId).Should().Be((Width, 180));
    }

    [Fact]
    public async Task Motion_Is_Over_Only_After_The_Scene_Has_Been_Quiet_For_The_Post_Motion_Time()
    {
        // Something that moves stops and starts; an end reported at every
        // pause would start and stop a recording many times a minute.
        await ShowAsync(EmptyScene, SceneWithBlock);
        events.Clear();

        clock.Advance(TimeSpan.FromSeconds(4));
        await ShowAsync(SceneWithBlock);
        clock.Advance(TimeSpan.FromSeconds(5));
        await ShowAsync(SceneWithBlock);

        events.Should().BeEmpty("the scene has been quiet for 9 of the 10 seconds");
        sut.IsMotionDetected(cameraId).Should().BeTrue();

        clock.Advance(TimeSpan.FromSeconds(1));
        await ShowAsync(SceneWithBlock);

        var end = events.Should().ContainSingle().Subject;
        end.IsMotionActive.Should().BeFalse();
        end.BoundingBoxes.Should().BeEmpty();
        sut.IsMotionDetected(cameraId).Should().BeFalse();
        sut.GetLastBoundingBoxes(cameraId).Should().BeEmpty();
    }

    [Fact]
    public async Task Movement_During_The_Quiet_Time_Keeps_The_Motion_Going()
    {
        await ShowAsync(EmptyScene, SceneWithBlock);
        clock.Advance(TimeSpan.FromSeconds(8));
        await ShowAsync(EmptyScene);
        clock.Advance(TimeSpan.FromSeconds(8));
        await ShowAsync(EmptyScene);

        events.Should().HaveCount(2).And.OnlyContain(e => e.IsMotionActive);
        sut.IsMotionDetected(cameraId).Should().BeTrue();
    }

    [Fact]
    public async Task A_Change_Below_The_Minimum_Is_Not_Motion()
    {
        settings.MinimumChangePercent = 50;

        await ShowAsync(EmptyScene, SceneWithBlock);

        events.Should().BeEmpty();
    }

    [Fact]
    public async Task A_Picture_That_Cannot_Be_Captured_Or_Read_Is_Skipped()
    {
        await ShowAsync(EmptyScene);
        pipeline.CaptureFrameAsync(Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        await sut.AnalyzeOnceAsync(cameraId, TestContext.Current.CancellationToken);
        await ShowAsync(Unreadable);
        pipeline.CaptureFrameAsync(Arg.Any<CancellationToken>()).Returns<byte[]?>(_ => throw new InvalidOperationException("camera gone"));
        await sut.AnalyzeOnceAsync(cameraId, TestContext.Current.CancellationToken);

        await ShowAsync(SceneWithBlock);

        events.Should().ContainSingle("the block is compared with the last picture that could be read")
            .Which.IsMotionActive.Should().BeTrue();
    }

    [Fact]
    public async Task A_Pipeline_That_Was_Closed_Ends_The_Detection_And_The_Motion()
    {
        // Whoever owns the pipeline closed it (the camera was unplugged, or
        // its recording was stopped). Going on asking it for pictures would
        // fail four times a second for ever.
        await ShowAsync(EmptyScene, SceneWithBlock);
        events.Clear();
        pipeline.CaptureFrameAsync(Arg.Any<CancellationToken>()).Returns<byte[]?>(_ => throw new ObjectDisposedException("VideoPlayer"));

        await sut.AnalyzeOnceAsync(cameraId, TestContext.Current.CancellationToken);

        sut.IsDetectionActive(cameraId).Should().BeFalse();
        events.Should().ContainSingle().Which.IsMotionActive.Should().BeFalse();
    }

    [Fact]
    public async Task A_Camera_That_Is_No_Longer_Watched_Raises_Nothing()
    {
        await ShowAsync(EmptyScene);
        sut.StopDetection(cameraId);

        await ShowAsync(SceneWithBlock);

        events.Should().BeEmpty();
        sut.IsDetectionActive(cameraId).Should().BeFalse();
    }

    [Fact]
    public async Task Stopping_While_Something_Moves_Reports_The_End_Of_The_Motion()
    {
        // Whoever started a recording on the motion has to hear that it is over.
        await ShowAsync(EmptyScene, SceneWithBlock);
        events.Clear();

        sut.StopDetection(cameraId);

        events.Should().ContainSingle().Which.IsMotionActive.Should().BeFalse();
    }

    [Fact]
    public async Task The_Default_Detector_Reports_Something_That_Travels_With_A_Track()
    {
        var walker = new WalkingBlockReader();
        using var service = new ServerMotionDetectionService(
            NullLogger<ServerMotionDetectionService>.Instance,
            walker,
            clock,
            runLoops: false);
        var seen = new List<MotionDetectedEventArgs>();
        service.MotionDetected += (_, e) => seen.Add(e);
        service.StartDetection(cameraId, pipeline, settings);

        for (var step = 0; step < 12; step++)
        {
            walker.Step = step;
            pipeline.CaptureFrameAsync(Arg.Any<CancellationToken>()).Returns(new byte[] { 1 });
            await service.AnalyzeOnceAsync(cameraId, TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromMilliseconds(250));
        }

        seen.Should().Contain(e => e.IsMotionActive && e.BoundingBoxes.Count == 1);
        seen.Where(e => e.IsMotionActive).SelectMany(e => e.BoundingBoxes).Select(b => b.TrackId).Distinct()
            .Should().ContainSingle().Which.Should().NotBeNull();
    }

    [Fact]
    public async Task A_Camera_Analysed_Only_For_Its_Index_Reports_No_Motion_But_Every_Picture()
    {
        // A camera that is recorded but not watched for motion: no badge, no webhook, no motion
        // recording may come of it; the activity index still needs every picture.
        var other = Guid.NewGuid();
        var analysed = new List<PictureAnalyzedEventArgs>();
        sut.PictureAnalyzed += (_, e) => analysed.Add(e);
        sut.StartIndexing(other, pipeline, settings);

        foreach (var scene in new[] { EmptyScene, SceneWithBlock, SceneWithBlock })
        {
            pipeline.CaptureFrameAsync(Arg.Any<CancellationToken>()).Returns(new[] { scene });
            await sut.AnalyzeOnceAsync(other, TestContext.Current.CancellationToken);
        }

        events.Should().BeEmpty();
        sut.IsDetectionActive(other).Should().BeFalse("the camera is not watched for motion");
        sut.IsMotionDetected(other).Should().BeFalse();
        sut.IsIndexing(other).Should().BeTrue();
        analysed.Should().HaveCount(3).And.OnlyContain(e => e.CameraId == other);
        analysed[1].Result.ChangePercent.Should().BeGreaterThan(5);
    }

    [Fact]
    public async Task A_Camera_Watched_Again_While_Indexed_Reports_Motion_And_Stops_Reporting_When_No_Longer_Watched()
    {
        sut.StopDetection(cameraId);
        sut.StartIndexing(cameraId, pipeline, settings);
        sut.StartDetection(cameraId, pipeline, settings);

        await ShowAsync(EmptyScene, SceneWithBlock);
        events.Should().ContainSingle().Which.IsMotionActive.Should().BeTrue();
        events.Clear();

        sut.StopDetection(cameraId);

        events.Should().ContainSingle().Which.IsMotionActive.Should().BeFalse("whoever acted on the motion hears it is over");
        sut.IsDetectionActive(cameraId).Should().BeFalse();
        sut.IsIndexing(cameraId).Should().BeTrue("the recording still needs its index");

        events.Clear();
        await ShowAsync(EmptyScene, SceneWithBlock);
        events.Should().BeEmpty();
    }

    [Fact]
    public void The_Zones_Reach_The_Detector_At_The_Start_And_While_It_Runs()
    {
        var detector = Substitute.For<IMotionDetector>();
        using var service = new ServerMotionDetectionService(
            NullLogger<ServerMotionDetectionService>.Instance,
            new BlockReader(),
            clock,
            runLoops: false,
            createDetector: _ => detector);
        IReadOnlyList<CoreMotionZone> first = [new() { Name = "Tree" }];
        IReadOnlyList<CoreMotionZone> edited = [new() { Name = "Road" }];

        service.StartDetection(cameraId, pipeline, settings, first);
        service.UseZones(cameraId, edited);
        service.UseZones(Guid.NewGuid(), edited);

        Received.InOrder(() =>
        {
            detector.UseZones(first);
            detector.UseZones(edited);
        });
        detector.ReceivedCalls().Should().HaveCount(2, "a camera that is not analysed has no detector to tell");
    }

    [Fact]
    public void A_Camera_Watched_For_Motion_Goes_On_When_Its_Index_Ends()
    {
        sut.StartIndexing(cameraId, pipeline, settings);

        sut.StopIndexing(cameraId);

        sut.IsDetectionActive(cameraId).Should().BeTrue();
        sut.IsIndexing(cameraId).Should().BeFalse();
    }

    [Fact]
    public async Task A_Camera_Analysed_Only_For_Its_Index_Is_Let_Go_When_The_Index_Ends()
    {
        var other = Guid.NewGuid();
        var analysed = new List<PictureAnalyzedEventArgs>();
        sut.PictureAnalyzed += (_, e) => analysed.Add(e);
        sut.StartIndexing(other, pipeline, settings);

        sut.StopIndexing(other);
        pipeline.CaptureFrameAsync(Arg.Any<CancellationToken>()).Returns(new[] { SceneWithBlock });
        await sut.AnalyzeOnceAsync(other, TestContext.Current.CancellationToken);

        sut.IsIndexing(other).Should().BeFalse();
        analysed.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(2, 500)]
    [InlineData(15, 250)]
    [InlineData(30, 250)]
    public void Pictures_Are_Analysed_At_The_Chosen_Rate_But_No_More_Than_Four_A_Second(
        int analysisFrameRate,
        int expectedMilliseconds)
        => ServerMotionDetectionService.GetInterval(new MotionDetectionSettings { AnalysisFrameRate = analysisFrameRate })
            .TotalMilliseconds.Should().Be(expectedMilliseconds);

    private async Task ShowAsync(params byte[] scenes)
    {
        foreach (var scene in scenes)
        {
            pipeline.CaptureFrameAsync(Arg.Any<CancellationToken>()).Returns(new[] { scene });
            await sut.AnalyzeOnceAsync(cameraId, TestContext.Current.CancellationToken);
        }
    }

    private sealed class BlockReader : IMotionFrameReader
    {
        public byte[]? ReadGrayscale(
            byte[] encodedImage,
            int width,
            int height)
        {
            if (encodedImage[0] == Unreadable)
            {
                return null;
            }

            var picture = new byte[width * height];
            Array.Fill(picture, EmptyScene);
            if (encodedImage[0] != EmptyScene)
            {
                for (var row = 60; row < 140; row++)
                {
                    Array.Fill(picture, encodedImage[0], (row * width) + 100, 100);
                }
            }

            return picture;
        }
    }

    // A block that walks right by 12 pixels a picture, from the second picture on.
    private sealed class WalkingBlockReader : IMotionFrameReader
    {
        public int Step { get; set; }

        public byte[]? ReadGrayscale(
            byte[] encodedImage,
            int width,
            int height)
        {
            var picture = new byte[width * height];
            Array.Fill(picture, EmptyScene);
            if (Step > 0)
            {
                var left = 20 + (Step * 12);
                for (var row = 80; row < 160; row++)
                {
                    Array.Fill(picture, SceneWithBlock, (row * width) + left, 30);
                }
            }

            return picture;
        }
    }

    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }
}