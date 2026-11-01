namespace AtcSoft.Video.Surveillance.Wpf.ViewModels;

/// <summary>
/// Loading the live view again keeps the tile, player and stream of every
/// camera that stays; only cameras that came or went change. Loads never
/// overlap, and a full screen window goes with its tile.
/// </summary>
public sealed class LiveViewViewModelReloadTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly global::VideoSurveillance.Generated.Cameras.Models.Camera front = TestCameras.Network() with { DisplayName = "Front" };
    private readonly global::VideoSurveillance.Generated.Cameras.Models.Camera back = TestCameras.Network() with { DisplayName = "Back" };
    private readonly global::VideoSurveillance.Generated.Cameras.Models.Camera gate = TestCameras.Network() with { DisplayName = "Gate" };
    private readonly SurveillanceEventService events = TestEvents.Service();
    private readonly IVideoPlayerFactory players = Substitute.For<IVideoPlayerFactory>();
    private readonly List<string> requests = [];
    private readonly GatewayTestServer server;
    private global::VideoSurveillance.Generated.Cameras.Models.Camera[] cameras;
    private Func<Task>? beforeCameraList;

    public LiveViewViewModelReloadTests()
    {
        cameras = [front, back];
        players.Create().Returns(_ => new StrictVideoPlayer());
        server = new GatewayTestServer(Answer);
    }

    public void Dispose()
    {
        server.Dispose();
        events.Dispose();
    }

    [Fact]
    public async Task A_Camera_That_Stays_Keeps_Its_Tile_And_Stream_And_Only_Its_Lease_Is_Checked()
    {
        using var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        var frontTile = sut.CameraTiles.Single(t => t.CameraId == front.Id);
        var frontPlayer = frontTile.Player;
        var backTile = sut.CameraTiles.Single(t => t.CameraId == back.Id);
        requests.Clear();

        cameras = [front with { DisplayName = "Front door" }, gate];
        await sut.LoadCommand.ExecuteAsync(null);

        sut.CameraTiles.Select(t => t.DisplayName).Should().Equal("Front door", "Gate");
        sut.CameraTiles[0].Should().BeSameAs(frontTile);
        frontTile.Player.Should().BeSameAs(frontPlayer);
        backTile.Player.Should().BeNull("the tile of a camera that went is closed");
        await WaitForAsync(() => Requested($"DELETE /cameras/{back.Id}/stream/"));
        Requested($"PUT /cameras/{front.Id}/stream/").Should().BeTrue();
        Requested($"POST /cameras/{gate.Id}/stream").Should().BeTrue();
        Requested($"POST /cameras/{front.Id}/stream").Should().BeFalse();
    }

    private bool Requested(string request)
    {
        lock (requests)
        {
            return requests.Contains(request, StringComparer.Ordinal);
        }
    }

    [Fact]
    public async Task A_Server_That_Cannot_Be_Reached_Keeps_The_Wall()
    {
        using var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        CameraTileViewModel[] tiles = [.. sut.CameraTiles];

        cameras = null!;
        await sut.LoadCommand.ExecuteAsync(null);

        sut.CameraTiles.Should().Equal(tiles);
        tiles.Should().OnlyContain(t => t.Player != null);
    }

    [Fact]
    public async Task A_Load_Asked_For_While_One_Runs_Runs_Once_After_It()
    {
        using var sut = CreateSut();
        var release = new TaskCompletionSource();
        var listed = 0;
        beforeCameraList = async () =>
        {
            if (Interlocked.Increment(ref listed) == 1)
            {
                await release.Task.ConfigureAwait(false);
            }
        };

        // The test server answers on the caller's thread: the first load waits on a thread of its own.
        var first = Task.Run(() => sut.LoadCommand.ExecuteAsync(null), TestContext.Current.CancellationToken);
        await WaitForAsync(() => listed == 1);
        var second = sut.LoadCommand.ExecuteAsync(null);
        var third = sut.LoadCommand.ExecuteAsync(null);
        listed.Should().Be(1, "a load does not start while one runs");

        release.SetResult();
        await Task.WhenAll(first, second, third);

        listed.Should().Be(2, "the loads asked for meanwhile run once, after it");
        sut.CameraTiles.Should().HaveCount(2);
    }

    [Fact]
    public void A_Full_Screen_Window_Goes_With_Its_Tile()
    {
        var tile = new CameraTileViewModel(players, Substitute.For<IStreamLeaseGateway>(), events, "http://server.test");
        var closed = 0;
        using var following = LiveViewViewModel.WhenTileCloses(tile, () => closed++);

        tile.Rotation = AtcSoft.Video.Surveillance.Enums.CameraRotation.Rotate90;
        closed.Should().Be(0);

        tile.Dispose();
        tile.Dispose();

        closed.Should().Be(1);
    }

    private LiveViewViewModel CreateSut()
        => new(
            server.Gateway,
            events,
            players,
            Substitute.For<AtcSoft.Video.Surveillance.Services.IUsbCameraWatcher>(),
            "http://server.test");

    private HttpResponseMessage Answer(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        lock (requests)
        {
            requests.Add($"{request.Method} {Shorten(path)}");
        }

        if (request.Method == HttpMethod.Get && path == "/cameras")
        {
            beforeCameraList?.Invoke().GetAwaiter().GetResult();
            return cameras is null
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : JsonAnswer(cameras);
        }

        if (request.Method == HttpMethod.Post && path.EndsWith("/stream", StringComparison.Ordinal))
        {
            var cameraId = Guid.Parse(path.Split('/')[2]);
            return JsonAnswer(new global::VideoSurveillance.Generated.Cameras.Models.StreamLease(
                LeaseId: Guid.NewGuid(),
                CameraId: cameraId,
                PlaylistUrl: $"/streams/{cameraId:N}/stream.m3u8",
                RenewSeconds: 30));
        }

        return new HttpResponseMessage(request.Method == HttpMethod.Get ? HttpStatusCode.NotFound : HttpStatusCode.NoContent)
        {
            RequestMessage = request,
        };
    }

    // Leaves the lease id out of a lease's address, so a test names it by camera.
    private static string Shorten(string path)
    {
        var at = path.IndexOf("/stream/", StringComparison.Ordinal);
        return at < 0 ? path : path[..(at + "/stream/".Length)];
    }

    private static HttpResponseMessage JsonAnswer<T>(T value)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value, Json), System.Text.Encoding.UTF8, "application/json"),
        };

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