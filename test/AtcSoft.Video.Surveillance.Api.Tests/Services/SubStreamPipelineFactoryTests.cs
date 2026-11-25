namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// The factory opens a camera with a sub-stream as two connections, its main stream undecoded,
/// unless the sub-stream failed lately; a snapshot or a recording of one stream gets that one.
/// </summary>
public sealed class SubStreamPipelineFactoryTests
{
    private readonly List<(Uri Uri, StreamOptions Options)> opened = [];
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider time = new(DateTimeOffset.Parse("2026-11-24T12:00:00Z", CultureInfo.InvariantCulture));
    private readonly SubStreamAvailability availability;
    private readonly VideoEngineMediaPipelineFactory sut;

    public SubStreamPipelineFactoryTests()
    {
        availability = new SubStreamAvailability(time);
        var players = Substitute.For<IVideoPlayerFactory>();
        players.Create().Returns(_ =>
        {
            var player = Substitute.For<IVideoPlayer>();
            player.When(p => p.Open(Arg.Any<Uri>(), Arg.Any<StreamOptions>()))
                .Do(call => opened.Add((call.Arg<Uri>(), call.Arg<StreamOptions>())));
            return player;
        });

        sut = new VideoEngineMediaPipelineFactory(players, subStreams: availability);
    }

    [Fact]
    public void A_Camera_With_A_Sub_Stream_Decodes_The_Sub_Stream_And_Records_The_Main()
    {
        using var pipeline = sut.Create(Camera("/stream2"));

        pipeline.Should().BeOfType<SubStreamMediaPipeline>();
        opened.Should().HaveCount(2);
        opened.Single(o => o.Uri.AbsolutePath == "/stream1").Options.DecodeVideo.Should().BeFalse("the main stream only records");
        opened.Single(o => o.Uri.AbsolutePath == "/stream2").Options.DecodeVideo.Should().BeTrue("the sub-stream is analysed");
    }

    [Fact]
    public void A_Camera_Without_A_Sub_Stream_Is_One_Decoded_Connection()
    {
        using var pipeline = sut.Create(Camera(subStreamPath: null));

        pipeline.Should().BeOfType<VideoEngineMediaPipeline>();
        opened.Should().ContainSingle().Which.Options.DecodeVideo.Should().BeTrue();
    }

    [Fact]
    public void A_Sub_Stream_That_Failed_Is_Left_Out_For_A_While_Then_Tried_Again()
    {
        var camera = Camera("/stream2");
        availability.Failed(camera.Id, "/stream2", "would not open");

        using (var pipeline = sut.Create(camera))
        {
            pipeline.Should().BeOfType<VideoEngineMediaPipeline>("the main stream alone, decoded");
        }

        time.Advance(SubStreamAvailability.RetryAfter);
        using (var pipeline = sut.Create(camera))
        {
            pipeline.Should().BeOfType<SubStreamMediaPipeline>();
        }
    }

    [Fact]
    public void Another_Sub_Stream_Path_Is_Tried_At_Once()
    {
        var camera = Camera("/stream2");
        availability.Failed(camera.Id, "/stream3", "would not open");

        using var pipeline = sut.Create(camera);

        pipeline.Should().BeOfType<SubStreamMediaPipeline>();
    }

    [Fact]
    public void One_Stream_Can_Be_Asked_For()
    {
        using var pipeline = sut.Create(Camera("/stream2"), CameraStreamRole.Main);

        pipeline.Should().BeOfType<VideoEngineMediaPipeline>();
        var (uri, options) = opened.Should().ContainSingle().Subject;
        uri.AbsolutePath.Should().Be("/stream1");
        options.DecodeVideo.Should().BeTrue("a snapshot needs the picture");
    }

    private static CameraConfiguration Camera(string? subStreamPath)
    {
        var camera = new CameraConfiguration();
        camera.Display.DisplayName = "Gate";
        camera.Connection.IpAddress = "192.0.2.10";
        camera.Connection.Path = "/stream1";
        camera.Connection.SubStreamPath = subStreamPath;
        return camera;
    }
}