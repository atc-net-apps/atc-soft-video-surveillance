namespace Atc.Video.Engine;

/// <summary>
/// A player owns its graphics accelerator (a D3D11 device with its textures): disposing the player
/// disposes it. Before, every player a tile, a reconnect or a connection test made leaked one.
/// </summary>
public sealed class VideoPlayerDisposeTests
{
    [Fact]
    public void Disposing_A_Player_Disposes_Its_Graphics_Accelerator()
    {
        var accelerator = Substitute.For<IGpuAccelerator>();
        var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance, accelerator);

        player.Dispose();

        accelerator.Received(1).Dispose();
    }

    [Fact]
    public async Task A_State_Subscriber_That_Throws_Does_Not_End_The_Stream_Thread_Or_Keep_The_Others_From_Hearing()
    {
        // The error is raised from the demux thread's own error handling: a throwing subscriber
        // escaped the thread there, and the process ended.
        TestEngine.Require();
        using var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance);
        var heard = new TaskCompletionSource<PlayerState>(TaskCreationOptions.RunContinuationsAsynchronously);
        player.StateChanged += (_, e) =>
        {
            if (e.NewState == PlayerState.Error)
            {
                throw new InvalidOperationException("A subscriber that fails");
            }
        };
        player.StateChanged += (_, e) =>
        {
            if (e.NewState == PlayerState.Error)
            {
                heard.TrySetResult(e.NewState);
            }
        };

        // Nothing listens on this port: the open fails at once.
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        player.Open(new Uri($"tcp://127.0.0.1:{port}"), new StreamOptions { HardwareAcceleration = false });

        (await heard.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken)).Should().Be(PlayerState.Error);
    }

    [Fact]
    public void Disposing_A_Player_Twice_Disposes_Its_Accelerator_Once()
    {
        var accelerator = Substitute.For<IGpuAccelerator>();
        var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance, accelerator);

        player.Dispose();
        player.Dispose();

        accelerator.Received(1).Dispose();
    }
}