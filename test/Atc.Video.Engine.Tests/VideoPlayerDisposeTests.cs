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
    public void Disposing_A_Player_Twice_Disposes_Its_Accelerator_Once()
    {
        var accelerator = Substitute.For<IGpuAccelerator>();
        var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance, accelerator);

        player.Dispose();
        player.Dispose();

        accelerator.Received(1).Dispose();
    }
}