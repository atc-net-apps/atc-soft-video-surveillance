namespace AtcSoft.VideoSurveillance.Wpf.ViewModels;

/// <summary>
/// A tile watches its camera on a lease: it plays the playlist the lease
/// names, gives the lease back when it stops, and starts again when the
/// server no longer has the lease.
/// </summary>
public sealed class CameraTileViewModelStreamTests : IDisposable
{
    private readonly Guid cameraId = Guid.NewGuid();
    private readonly Guid leaseId = Guid.NewGuid();
    private readonly IStreamLeaseGateway streams = Substitute.For<IStreamLeaseGateway>();
    private readonly IVideoPlayer player = Substitute.For<IVideoPlayer>();
    private readonly SurveillanceEventService events = TestEvents.Service();
    private readonly CameraTileViewModel sut;

    public CameraTileViewModelStreamTests()
    {
        var factory = Substitute.For<IVideoPlayerFactory>();
        factory.Create().Returns(player);
        streams
            .StartStreamAsync(cameraId, Arg.Any<CancellationToken>())
            .Returns(new global::VideoSurveillance.Generated.Cameras.Models.StreamLease(LeaseId: leaseId, CameraId: cameraId, PlaylistUrl: "/streams/front/stream.m3u8", RenewSeconds: 30));

        sut = new CameraTileViewModel(factory, streams, events, "http://server.test/") { CameraId = cameraId };
    }

    public void Dispose()
    {
        sut.Dispose();
        events.Dispose();
        player.Dispose();
    }

    [Fact]
    public async Task Starting_Takes_A_Lease_And_Plays_The_Playlist_It_Names()
    {
        await sut.StartStreamAsync();

        await WaitForAsync(() => player.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IVideoPlayer.Open)));
        player.Received(1).Open(new Uri("http://server.test/streams/front/stream.m3u8"), Arg.Any<StreamOptions>());
    }

    [Fact]
    public async Task Stopping_Gives_The_Lease_Back()
    {
        await sut.StartStreamAsync();

        await sut.StopStreamAsync();
        await sut.StopStreamAsync();

        await streams.Received(1).StopStreamAsync(cameraId, leaseId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_Lease_Is_Renewed_While_The_Tile_Watches()
    {
        streams.RenewStreamLeaseAsync(cameraId, leaseId, Arg.Any<CancellationToken>()).Returns(StreamLeaseRenewal.Renewed);
        await sut.StartStreamAsync();

        await sut.RenewLeaseAsync();

        await streams.Received(1).RenewStreamLeaseAsync(cameraId, leaseId, Arg.Any<CancellationToken>());
        await streams.Received(1).StartStreamAsync(cameraId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_Lease_The_Server_No_Longer_Has_Starts_The_Stream_Again()
    {
        // The server restarted, or the lease ran out while this computer slept.
        streams.RenewStreamLeaseAsync(cameraId, leaseId, Arg.Any<CancellationToken>()).Returns(StreamLeaseRenewal.Gone);
        await sut.StartStreamAsync();

        await sut.RenewLeaseAsync();

        await streams.Received(2).StartStreamAsync(cameraId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_Server_That_Is_Away_Keeps_The_Lease_For_The_Next_Renewal()
    {
        streams.RenewStreamLeaseAsync(cameraId, leaseId, Arg.Any<CancellationToken>()).Returns(StreamLeaseRenewal.Unreachable);
        await sut.StartStreamAsync();

        await sut.RenewLeaseAsync();
        await sut.StopStreamAsync();

        await streams.Received(1).StartStreamAsync(cameraId, Arg.Any<CancellationToken>());
        await streams.Received(1).StopStreamAsync(cameraId, leaseId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_Tile_That_Is_Not_Watching_Renews_Nothing()
    {
        await sut.RenewLeaseAsync();

        await streams.DidNotReceive().RenewStreamLeaseAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Closing_The_App_Gives_Every_Tiles_Lease_Back_At_Once()
    {
        var otherCamera = Guid.NewGuid();
        var otherLease = Guid.NewGuid();
        streams
            .StartStreamAsync(otherCamera, Arg.Any<CancellationToken>())
            .Returns(new global::VideoSurveillance.Generated.Cameras.Models.StreamLease(LeaseId: otherLease, CameraId: otherCamera, PlaylistUrl: "/streams/back/stream.m3u8", RenewSeconds: 30));
        var factory = Substitute.For<IVideoPlayerFactory>();
        factory.Create().Returns(_ => Substitute.For<IVideoPlayer>());
        using var other = new CameraTileViewModel(factory, streams, events, "http://server.test/") { CameraId = otherCamera };
        await sut.StartStreamAsync();
        await other.StartStreamAsync();

        // The server is slow: both are given back without waiting for each other.
        var release = new TaskCompletionSource();
        streams.StopStreamAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(release.Task);
        var stopping = CameraTileViewModel.StopAllAsync([sut, other]);

        await streams.Received(1).StopStreamAsync(cameraId, leaseId, Arg.Any<CancellationToken>());
        await streams.Received(1).StopStreamAsync(otherCamera, otherLease, Arg.Any<CancellationToken>());
        stopping.IsCompleted.Should().BeFalse();
        release.SetResult();
        await stopping;
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }

        condition().Should().BeTrue();
    }
}