namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// A viewer watches on a lease it renews: the stream runs while one lease on
/// it is alive, and a viewer that goes away without a word lets its lease
/// run out instead of keeping the stream up.
/// </summary>
public sealed class StreamingServiceLeaseTests : IDisposable
{
    private readonly CameraConfiguration camera = new() { Id = Guid.NewGuid() };
    private readonly ICameraStorageService storage = Substitute.For<ICameraStorageService>();
    private readonly ICameraPipelineHost host = Substitute.For<ICameraPipelineHost>();
    private readonly IMediaPipeline pipeline = Substitute.For<IMediaPipeline>();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider time = new(DateTimeOffset.Parse("2026-10-15T12:00:00Z", CultureInfo.InvariantCulture));
    private readonly StreamingService sut;

    public StreamingServiceLeaseTests()
    {
        camera.Connection.Source = CoreCameraSource.Usb;
        camera.Connection.Usb = new UsbConnectionSettings { DeviceId = "usb#1", FriendlyName = "Webcam" };
        storage.GetCameraById(camera.Id).Returns(camera);
        host.Hosts(camera).Returns(true);
        host.Acquire(camera, CameraPipelineUse.Viewing).Returns(pipeline);
        pipeline.FramesDecoded.Returns(100);
        pipeline.IsLiveStreamActive.Returns(true);

        // The pipeline writes the playlist and a first segment as soon as it streams.
        pipeline
            .When(p => p.StartLiveStream(Arg.Any<string>()))
            .Do(call =>
            {
                var playlist = call.Arg<string>();
                File.WriteAllText(playlist, "#EXTM3U");
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(playlist)!, "segment0.ts"), "ts");
            });

        sut = new StreamingService(storage, NullLogger<StreamingService>.Instance, host, time);
    }

    public void Dispose()
    {
        sut.Dispose();
        pipeline.Dispose();
    }

    [Fact]
    public async Task A_Viewer_Gets_A_Lease_And_The_Playlist_When_The_Stream_Can_Be_Played()
    {
        var start = await sut.StartAsync(camera.Id, TestContext.Current.CancellationToken);

        start.Outcome.Should().Be(LiveStreamOutcome.Started);
        start.LeaseId.Should().NotBeEmpty();
        start.PlaylistPath.Should().Be($"/streams/{start.LeaseId:N}/stream.m3u8");
        sut.TryGetLeaseFolder(start.LeaseId, out var folder).Should().BeTrue();
        folder.Should().Be(Path.Combine(sut.HlsOutputRoot, camera.Id.ToString("N")));
        sut.TryGetLeaseFolder(Guid.NewGuid(), out _).Should().BeFalse();
        sut.GetViewerCount(camera.Id).Should().Be(1);
    }

    [Fact]
    public async Task The_Stream_Runs_Until_The_Last_Lease_On_It_Is_Given_Back()
    {
        var first = await sut.StartAsync(camera.Id, TestContext.Current.CancellationToken);
        var second = await sut.StartAsync(camera.Id, TestContext.Current.CancellationToken);

        sut.Release(camera.Id, first.LeaseId);
        sut.Release(camera.Id, first.LeaseId);
        sut.IsStreaming(camera.Id).Should().BeTrue("a lease given back twice counts once");

        sut.Release(camera.Id, second.LeaseId);
        sut.IsStreaming(camera.Id).Should().BeFalse();
        sut.Renew(camera.Id, second.LeaseId).Should().BeFalse();
    }

    [Fact]
    public async Task A_Lease_That_Is_Not_Renewed_Runs_Out_And_The_Stream_Stops_With_It()
    {
        var start = await sut.StartAsync(camera.Id, TestContext.Current.CancellationToken);

        time.Advance((sut.RenewInterval * 2) + TimeSpan.FromSeconds(1));
        sut.ReapIdleSessions();

        sut.IsStreaming(camera.Id).Should().BeFalse();
        sut.Renew(camera.Id, start.LeaseId).Should().BeFalse("the viewer starts again");
    }

    [Fact]
    public async Task A_Renewed_Lease_Keeps_The_Stream_Running()
    {
        var start = await sut.StartAsync(camera.Id, TestContext.Current.CancellationToken);

        for (var i = 0; i < 4; i++)
        {
            time.Advance(sut.RenewInterval);
            sut.Renew(camera.Id, start.LeaseId).Should().BeTrue();
            sut.ReapIdleSessions();
        }

        sut.IsStreaming(camera.Id).Should().BeTrue();
    }

    [Fact]
    public async Task A_Stale_Lease_From_An_Earlier_Stream_Does_Not_Stop_A_Later_One()
    {
        var old = await sut.StartAsync(camera.Id, TestContext.Current.CancellationToken);
        time.Advance((sut.RenewInterval * 2) + TimeSpan.FromSeconds(1));
        sut.ReapIdleSessions();

        var current = await sut.StartAsync(camera.Id, TestContext.Current.CancellationToken);
        sut.Release(camera.Id, old.LeaseId);

        sut.IsStreaming(camera.Id).Should().BeTrue();
        sut.Renew(camera.Id, current.LeaseId).Should().BeTrue();
    }

    [Fact]
    public async Task A_Lease_Is_Renewed_Only_On_Its_Own_Camera()
    {
        var start = await sut.StartAsync(camera.Id, TestContext.Current.CancellationToken);

        sut.Renew(Guid.NewGuid(), start.LeaseId).Should().BeFalse();
        sut.Release(Guid.NewGuid(), start.LeaseId);

        sut.IsStreaming(camera.Id).Should().BeTrue();
    }

    [Fact]
    public async Task A_Camera_The_Server_Does_Not_Know_Gets_No_Lease()
    {
        var start = await sut.StartAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        start.Outcome.Should().Be(LiveStreamOutcome.CameraNotFound);
    }

    [Fact]
    public async Task A_Camera_That_Cannot_Be_Opened_Gets_No_Lease_And_Leaves_No_Stream_Behind()
    {
        host.Acquire(camera, CameraPipelineUse.Viewing).Returns((IMediaPipeline?)null);

        var start = await sut.StartAsync(camera.Id, TestContext.Current.CancellationToken);

        start.Outcome.Should().Be(LiveStreamOutcome.Failed);
        start.Reason.Should().Contain("unplugged");
        sut.IsStreaming(camera.Id).Should().BeFalse();
    }

    [Fact]
    public async Task A_Stream_That_Ends_Before_It_Can_Be_Played_Gets_No_Lease()
    {
        // The camera was unplugged while the viewer waited.
        pipeline.FramesDecoded.Returns(0);
        var starting = sut.StartAsync(camera.Id, TestContext.Current.CancellationToken);
        pipeline.ConnectionStateChanged += Raise.EventWith(
            new ConnectionStateChangedEventArgs(ConnectionState.Connecting, ConnectionState.Error));

        var start = await starting;

        start.Outcome.Should().Be(LiveStreamOutcome.Failed);
        sut.IsStreaming(camera.Id).Should().BeFalse();
    }
}