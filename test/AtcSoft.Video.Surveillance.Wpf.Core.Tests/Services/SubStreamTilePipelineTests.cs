namespace AtcSoft.Video.Surveillance.Wpf.Core.Services;

using CoreConnectionState = AtcSoft.Video.Surveillance.Enums.ConnectionState;
using CoreConnectionStateChangedEventArgs = AtcSoft.Video.Surveillance.Events.ConnectionStateChangedEventArgs;

/// <summary>
/// A CameraWall tile whose camera has a sub-stream: the tile plays the sub-stream, and its
/// pipeline holds the main stream, undecoded, for everything that records, while it is needed.
/// </summary>
public sealed class SubStreamTilePipelineTests
{
    private static readonly Uri MainUri = new("rtsp://192.0.2.10:554/stream1");

    private readonly IMediaPipeline sub = Substitute.For<IMediaPipeline>();
    private readonly IVideoPlayer mainPlayer = Substitute.For<IVideoPlayer>();
    private readonly IMediaPipeline main = Substitute.For<IMediaPipeline>();

    [Fact]
    public void A_Tile_That_Is_Not_Connected_Holds_No_Main_Stream()
    {
        // Auto-connect off, or a tile the user stopped: no hidden connection to the camera.
        using var sut = CreateSut();

        mainPlayer.DidNotReceive().Open(Arg.Any<Uri>(), Arg.Any<StreamOptions>());
    }

    [Fact]
    public void The_Main_Stream_Opens_Undecoded_When_The_Sub_Stream_Plays()
    {
        using var sut = CreateSut();

        SubGoes(CoreConnectionState.Connected);

        mainPlayer.Received(1).Open(MainUri, Arg.Is<StreamOptions>(o => !o.DecodeVideo));
    }

    [Fact]
    public void The_Main_Stream_Closes_When_The_Tile_Stream_Stops()
    {
        using var sut = CreateSut();
        SubGoes(CoreConnectionState.Connected);

        SubGoes(CoreConnectionState.Disconnected);

        mainPlayer.Received(1).Close();
    }

    [Fact]
    public void A_Recording_Asked_For_Before_The_Main_Stream_Plays_Starts_With_It()
    {
        // The tile records on connect when its own (sub-)stream plays; the main stream plays a moment later.
        using var sut = CreateSut();
        SubGoes(CoreConnectionState.Connected);
        mainPlayer.State.Returns(PlayerState.Opening);

        sut.StartRecording("first.mkv");

        sut.IsRecordingActive.Should().BeTrue("the recording waits, so nothing starts it twice");
        main.DidNotReceive().StartRecording(Arg.Any<string>());

        mainPlayer.State.Returns(PlayerState.Playing);
        MainGoes(PlayerState.Playing);

        main.Received(1).StartRecording("first.mkv");
    }

    [Fact]
    public void A_Recording_Of_A_Playing_Main_Stream_Starts_At_Once_With_Its_Pre_Roll()
    {
        using var sut = CreateSut();
        SubGoes(CoreConnectionState.Connected);
        mainPlayer.State.Returns(PlayerState.Playing);
        var from = DateTime.UtcNow.AddSeconds(-5);

        sut.StartRecording("motion.mkv", from);

        main.Received(1).StartRecording("motion.mkv", from);
    }

    [Fact]
    public async Task A_Main_Stream_That_Drops_While_Recording_Is_Opened_Again()
    {
        using var sut = CreateSut(reopenDelay: TimeSpan.FromMilliseconds(50));
        SubGoes(CoreConnectionState.Connected);
        main.IsRecordingActive.Returns(true);

        MainGoes(PlayerState.Error);

        await WaitForOpensAsync(2);
        mainPlayer.Received(2).Open(MainUri, Arg.Any<StreamOptions>());
    }

    [Fact]
    public async Task A_Main_Stream_That_Drops_After_The_Tile_Stopped_Is_Left_Closed()
    {
        using var sut = CreateSut(reopenDelay: TimeSpan.FromMilliseconds(50));
        SubGoes(CoreConnectionState.Connected);
        SubGoes(CoreConnectionState.Disconnected);

        MainGoes(PlayerState.Error);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        mainPlayer.Received(1).Open(Arg.Any<Uri>(), Arg.Any<StreamOptions>());
    }

    [Fact]
    public void A_Recording_Is_Written_Only_While_Both_Streams_Arrive()
    {
        using var sut = CreateSut();
        var now = DateTime.UtcNow;
        sub.LastPacketUtc.Returns(now);
        main.LastPacketUtc.Returns(DateTime.MinValue);

        sut.LastPacketUtc.Should().Be(DateTime.MinValue, "the main stream sends nothing yet");

        main.LastPacketUtc.Returns(now.AddSeconds(-40));
        sut.LastPacketUtc.Should().Be(now.AddSeconds(-40), "the older of the two");
    }

    [Fact]
    public async Task The_Pictures_Are_Of_The_Sub_Stream_In_The_Main_Stream_Shape()
    {
        using var sut = CreateSut();
        main.DisplaySize.Returns((1920, 1080));
        sub.DisplaySize.Returns((640, 480));

        await sut.CaptureGreyAsync(160, 90, TestContext.Current.CancellationToken);

        await sub.Received(1).CaptureGreyAsync(160, 90, Arg.Any<CancellationToken>());
        sut.DisplaySize.Should().Be((1920, 1080), "a squeezed sub-stream is analysed as the main stream shows it");
    }

    [Fact]
    public void Disposing_Lets_Go_Of_The_Main_Stream_And_Both_Pipelines()
    {
        var sut = CreateSut();

        sut.Dispose();

        main.Received(1).Dispose();
        mainPlayer.Received(1).Dispose();
        sub.Received(1).Dispose();
    }

    private void SubGoes(CoreConnectionState state)
        => sub.ConnectionStateChanged += Raise.EventWith(sub, new CoreConnectionStateChangedEventArgs(CoreConnectionState.Connecting, state));

    private void MainGoes(PlayerState state)
        => mainPlayer.StateChanged += Raise.EventWith(mainPlayer, new PlayerStateChangedEventArgs(PlayerState.Opening, state));

    private async Task WaitForOpensAsync(int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (mainPlayer.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IVideoPlayer.Open)) < count && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }
    }

    private SubStreamTilePipeline CreateSut(TimeSpan? reopenDelay = null)
        => new(sub, mainPlayer, _ => main, MainUri, new StreamOptions(), reopenDelay);
}