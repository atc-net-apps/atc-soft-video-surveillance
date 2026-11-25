namespace AtcSoft.Video.Surveillance.Wpf.Core.Services;

using CoreConnectionState = AtcSoft.Video.Surveillance.Enums.ConnectionState;
using CoreConnectionStateChangedEventArgs = AtcSoft.Video.Surveillance.Events.ConnectionStateChangedEventArgs;

/// <summary>
/// A CameraWall tile whose camera has a sub-stream: the tile plays the sub-stream, and its
/// pipeline holds the main stream, undecoded, for everything that records.
/// </summary>
public sealed class SubStreamTilePipelineTests
{
    private static readonly Uri MainUri = new("rtsp://192.0.2.10:554/stream1");

    private readonly IMediaPipeline sub = Substitute.For<IMediaPipeline>();
    private readonly IVideoPlayer mainPlayer = Substitute.For<IVideoPlayer>();
    private readonly IMediaPipeline main = Substitute.For<IMediaPipeline>();

    [Fact]
    public void The_Main_Stream_Is_Opened_Without_Decoding()
    {
        using var sut = CreateSut();

        mainPlayer.Received(1).Open(MainUri, Arg.Is<StreamOptions>(o => !o.DecodeVideo));
    }

    [Fact]
    public void The_Recordings_Are_Of_The_Main_Stream()
    {
        using var sut = CreateSut();
        var start = DateTime.UtcNow.AddSeconds(-4);
        main.FindRecordingStart(TimeSpan.FromSeconds(5)).Returns(start);

        sut.FindRecordingStart(TimeSpan.FromSeconds(5)).Should().Be(start);
        sut.StartRecording("motion.mkv", start);
        sut.StopRecording();

        main.Received(1).StartRecording("motion.mkv", start);
        main.Received(1).StopRecording();
        sub.DidNotReceive().StartRecording(Arg.Any<string>(), Arg.Any<DateTime>());
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
    public void The_Tile_Hears_Its_Own_Stream()
    {
        using var sut = CreateSut();
        var states = new List<CoreConnectionState>();
        sut.ConnectionStateChanged += (_, e) => states.Add(e.NewState);

        sub.ConnectionStateChanged += Raise.EventWith(sub, new CoreConnectionStateChangedEventArgs(CoreConnectionState.Connecting, CoreConnectionState.Connected));

        states.Should().Equal(CoreConnectionState.Connected);
    }

    [Fact]
    public async Task A_Main_Stream_That_Drops_Is_Opened_Again()
    {
        using var sut = CreateSut(reopenDelay: TimeSpan.FromMilliseconds(50));

        mainPlayer.StateChanged += Raise.EventWith(mainPlayer, new PlayerStateChangedEventArgs(PlayerState.Playing, PlayerState.Error));

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (mainPlayer.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IVideoPlayer.Open)) < 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        mainPlayer.Received(2).Open(MainUri, Arg.Any<StreamOptions>());
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

    private SubStreamTilePipeline CreateSut(TimeSpan? reopenDelay = null)
        => new(sub, mainPlayer, _ => main, MainUri, new StreamOptions(), reopenDelay);
}