namespace AtcSoft.Video.Surveillance.Wpf.ViewModels;

/// <summary>
/// A tile whose player fails starts its stream again on a new lease, with a
/// player that opens only from Stopped, as the engine's does.
/// </summary>
public sealed class CameraTileViewModelRecoveryTests : IDisposable
{
    private readonly Guid cameraId = Guid.NewGuid();
    private readonly IStreamLeaseGateway streams = Substitute.For<IStreamLeaseGateway>();
    private readonly StrictVideoPlayer player = new();
    private readonly SurveillanceEventService events = TestEvents.Service();
    private readonly FakeTimeProvider time = new();
    private readonly CameraTileViewModel sut;
    private int leasesGiven;

    public CameraTileViewModelRecoveryTests()
    {
        var factory = Substitute.For<IVideoPlayerFactory>();
        factory.Create().Returns(player);
        streams
            .StartStreamAsync(cameraId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var number = Interlocked.Increment(ref leasesGiven);
                return new global::VideoSurveillance.Generated.Cameras.Models.StreamLease(
                    LeaseId: Guid.NewGuid(),
                    CameraId: cameraId,
                    PlaylistUrl: $"/streams/{number}/stream.m3u8",
                    RenewSeconds: 30);
            });

        sut = new CameraTileViewModel(factory, streams, events, "http://server.test/", time) { CameraId = cameraId };
    }

    public void Dispose()
    {
        sut.Dispose();
        events.Dispose();
        player.Dispose();
    }

    [Fact]
    public async Task A_Player_That_Failed_Plays_Again_On_A_New_Lease()
    {
        await sut.StartStreamAsync();
        await WaitForAsync(() => player.State == PlayerState.Playing);

        // The stream ended (the server stopped it, a read timed out).
        player.Fail();
        sut.IsRetryScheduled.Should().BeTrue();
        time.Advance(TimeSpan.FromSeconds(5));

        await WaitForAsync(() => player.Opened.Count == 2 && player.State == PlayerState.Playing);
        player.OpenFailures.Should().Be(0);
        player.Opened[1].Should().Be(new Uri("http://server.test/streams/2/stream.m3u8"));
        sut.IsRetryScheduled.Should().BeFalse();
    }

    [Fact]
    public async Task A_Lease_The_Server_No_Longer_Has_Plays_Again_While_The_Player_Plays()
    {
        streams.RenewStreamLeaseAsync(cameraId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(StreamLeaseRenewal.Gone);
        await sut.StartStreamAsync();
        await WaitForAsync(() => player.State == PlayerState.Playing);

        await sut.RenewLeaseAsync();

        await WaitForAsync(() => player.Opened.Count == 2 && player.State == PlayerState.Playing);
        player.OpenFailures.Should().Be(0);
        sut.IsRetryScheduled.Should().BeFalse();
    }

    [Fact]
    public async Task A_Server_That_Cannot_Start_The_Stream_Is_Asked_Again()
    {
        await sut.StartStreamAsync();
        await WaitForAsync(() => player.State == PlayerState.Playing);
        streams
            .StartStreamAsync(cameraId, Arg.Any<CancellationToken>())
            .Returns<Task<global::VideoSurveillance.Generated.Cameras.Models.StreamLease>>(_ => throw new TaskCanceledException("The request timed out."));

        player.Fail();
        time.Advance(TimeSpan.FromSeconds(5));

        await WaitForAsync(() => sut.IsRetryScheduled && streams.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IStreamLeaseGateway.StartStreamAsync)) == 2);
    }

    [Fact]
    public async Task A_Stopped_Tile_Does_Not_Start_Again()
    {
        await sut.StartStreamAsync();
        await WaitForAsync(() => player.State == PlayerState.Playing);

        await sut.StopStreamAsync();
        time.Advance(TimeSpan.FromMinutes(1));

        sut.IsRetryScheduled.Should().BeFalse();
        player.Opened.Should().HaveCount(1);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }

        condition().Should().BeTrue();
    }
}