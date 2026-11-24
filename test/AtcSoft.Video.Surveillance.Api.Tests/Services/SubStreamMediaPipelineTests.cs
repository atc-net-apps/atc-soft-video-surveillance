namespace AtcSoft.Video.Surveillance.Api.Services;

using Atc.Video.Engine;

/// <summary>
/// A camera with a sub-stream is two connections used as one: the sub-stream gives the
/// pictures, the main stream records.
/// </summary>
public sealed class SubStreamMediaPipelineTests
{
    private readonly IVideoPlayer mainPlayer = Substitute.For<IVideoPlayer>();
    private readonly IVideoPlayer subPlayer = Substitute.For<IVideoPlayer>();
    private readonly List<string> failures = [];
    private int played;

    [Fact]
    public void The_Recordings_Are_Of_The_Main_Stream()
    {
        using var sut = CreateSut();
        var start = DateTime.UtcNow.AddSeconds(-3);
        mainPlayer.FindRecordingStart(TimeSpan.FromSeconds(5)).Returns(start);

        sut.FindRecordingStart(TimeSpan.FromSeconds(5)).Should().Be(start);
        sut.StartRecording("motion.mkv", start);
        sut.SwitchRecording("next.mkv");
        sut.StopRecording();

        mainPlayer.Received(1).StartRecording("motion.mkv", start);
        mainPlayer.Received(1).SwitchRecording("next.mkv");
        mainPlayer.Received(1).StopRecording();
        subPlayer.DidNotReceive().StartRecording(Arg.Any<string>(), Arg.Any<DateTime>());
    }

    [Fact]
    public async Task The_Pictures_And_The_Live_Grid_Are_Of_The_Sub_Stream()
    {
        using var sut = CreateSut();
        subPlayer.FramesDecoded.Returns(42);

        await sut.CaptureGreyAsync(160, 90, TestContext.Current.CancellationToken);
        await sut.CaptureFrameAsync(TestContext.Current.CancellationToken);
        sut.StartLiveStream("grid.m3u8");

        await subPlayer.Received(1).CaptureGreyAsync(160, 90, Arg.Any<CancellationToken>());
        await subPlayer.Received(1).CaptureFrameAsync(Arg.Any<CancellationToken>());
        subPlayer.Received(1).StartStreaming("grid.m3u8");
        mainPlayer.DidNotReceive().StartStreaming(Arg.Any<string>());
        sut.FramesDecoded.Should().Be(42);
    }

    [Fact]
    public void The_Picture_Size_Is_The_Main_Stream_So_A_Squeezed_Sub_Stream_Is_Analysed_In_The_Main_Shape()
    {
        using var sut = CreateSut();
        mainPlayer.StreamInfo.Returns(new VideoStreamInfo { Width = 1920, Height = 1080 });
        subPlayer.StreamInfo.Returns(new VideoStreamInfo { Width = 640, Height = 480 });

        sut.DisplaySize.Should().Be((1920, 1080));
    }

    [Fact]
    public void Either_Stream_Going_Silent_Is_The_Pipeline_Going_Silent()
    {
        using var sut = CreateSut();
        var now = DateTime.UtcNow;
        mainPlayer.LastPacketUtc.Returns(now);
        subPlayer.LastPacketUtc.Returns(now.AddMinutes(-2));

        sut.LastPacketUtc.Should().Be(now.AddMinutes(-2));
    }

    [Fact]
    public void The_State_Is_The_Main_Stream_And_A_Failing_Sub_Stream_Fails_The_Pipeline_Once()
    {
        using var sut = CreateSut();
        var states = new List<ConnectionState>();
        sut.ConnectionStateChanged += (_, e) => states.Add(e.NewState);

        Raise(mainPlayer, PlayerState.Playing);
        Raise(subPlayer, PlayerState.Playing);
        Raise(subPlayer, PlayerState.Error);
        Raise(subPlayer, PlayerState.Error);
        Raise(mainPlayer, PlayerState.Playing);

        states.Should().Equal(ConnectionState.Connected, ConnectionState.Error);
        failures.Should().ContainSingle();
        played.Should().Be(1);
    }

    [Fact]
    public void Disposing_Closes_Both_Streams()
    {
        var sut = CreateSut();

        sut.Dispose();

        mainPlayer.Received(1).Dispose();
        subPlayer.Received(1).Dispose();
    }

    private static void Raise(
        IVideoPlayer player,
        PlayerState state)
        => player.StateChanged += NSubstitute.Raise.EventWith(player, new PlayerStateChangedEventArgs(PlayerState.Opening, state));

    private SubStreamMediaPipeline CreateSut()
        => new(
            new VideoEngineMediaPipeline(mainPlayer, "Gate"),
            new VideoEngineMediaPipeline(subPlayer, "Gate (sub-stream)"),
            failures.Add,
            () => played++,
            "Gate");
}
