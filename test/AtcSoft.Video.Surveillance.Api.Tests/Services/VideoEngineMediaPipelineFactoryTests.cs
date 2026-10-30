namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// A camera is opened with the recording choice that applies to it: the
/// application's, or its own.
/// </summary>
public sealed class VideoEngineMediaPipelineFactoryTests
{
    private readonly Atc.Video.Engine.IVideoPlayerFactory playerFactory = Substitute.For<Atc.Video.Engine.IVideoPlayerFactory>();
    private readonly Atc.Video.Engine.IVideoPlayer player = Substitute.For<Atc.Video.Engine.IVideoPlayer>();
    private readonly IApplicationSettingsService settings = Substitute.For<IApplicationSettingsService>();
    private readonly RecordingSettings recording = new();
    private readonly CameraConfiguration camera = new() { Id = Guid.NewGuid() };
    private Atc.Video.Engine.StreamOptions? opened;

    public VideoEngineMediaPipelineFactoryTests()
    {
        camera.Connection.IpAddress = "10.0.0.1";
        settings.Recording.Returns(recording);
        playerFactory.Create().Returns(player);
        player.When(p => p.Open(Arg.Any<Uri>(), Arg.Any<Atc.Video.Engine.StreamOptions>()))
            .Do(call => opened = call.Arg<Atc.Video.Engine.StreamOptions>());
    }

    [Theory]
    [InlineData(VideoTranscodeCodec.None, null, false)]
    [InlineData(VideoTranscodeCodec.H264, null, true)]
    [InlineData(VideoTranscodeCodec.None, VideoTranscodeCodec.H264, true)]
    [InlineData(VideoTranscodeCodec.H264, VideoTranscodeCodec.None, false)]
    public void A_Camera_Is_Opened_To_Record_As_H264_When_It_Or_The_Application_Says_So(
        VideoTranscodeCodec applicationChoice,
        VideoTranscodeCodec? cameraChoice,
        bool expected)
    {
        recording.TranscodeVideoCodec = applicationChoice;
        camera.Overrides.Recording.TranscodeVideoCodec = cameraChoice;
        var sut = new VideoEngineMediaPipelineFactory(playerFactory, settings);

        using var pipeline = sut.Create(camera);

        opened.Should().NotBeNull();
        opened!.TranscodeRecordingToH264.Should().Be(expected);
    }
}