namespace AtcSoft.Video.Surveillance.Wpf.Core.Windows;

/// <summary>
/// Full screen shows the tile's own player instead of taking it: the tile keeps recording with it,
/// checking it and reconnecting it, the window follows when the tile gets another player, and
/// closing the window never closes the tile's player.
/// </summary>
public sealed class FullScreenCameraWindowViewModelTests
{
    [Fact]
    public void Full_Screen_Shows_The_Tiles_Player_And_Follows_It_When_The_Tile_Gets_Another()
    {
        var first = Player(PlayerState.Playing);
        using var sut = Create(first);

        sut.Player.Should().BeSameAs(first);
        sut.ConnectionState.Should().Be(Atc.Network.ConnectionState.Connected);

        // The tile reconnected with a new player (a screen unlock, a camera change).
        var second = Player(PlayerState.Opening);
        sut.FollowSharedPlayer(second);

        sut.Player.Should().BeSameAs(second);
        sut.ConnectionState.Should().Be(Atc.Network.ConnectionState.Connecting);

        // The window listens to the new player only.
        first.StateChanged += Raise.EventWith(first, new PlayerStateChangedEventArgs(PlayerState.Playing, PlayerState.Error));
        sut.ConnectionState.Should().Be(Atc.Network.ConnectionState.Connecting);
        second.StateChanged += Raise.EventWith(second, new PlayerStateChangedEventArgs(PlayerState.Opening, PlayerState.Playing));
        sut.ConnectionState.Should().Be(Atc.Network.ConnectionState.Connected);
    }

    [Fact]
    public void Closing_Full_Screen_Leaves_The_Tiles_Player_Alone()
    {
        var shared = Player(PlayerState.Playing);
        var sut = Create(shared);

        sut.Dispose();

        shared.DidNotReceive().Dispose();
        shared.DidNotReceive().Close();
    }

    private static FullScreenCameraWindowViewModel Create(IVideoPlayer shared)
    {
        var camera = new AtcSoft.Video.Surveillance.Wpf.Core.Models.CameraConfiguration();
        camera.Display.DisplayName = "Door";
        return new FullScreenCameraWindowViewModel(
            camera,
            showOverlayTitle: true,
            showOverlayDescription: false,
            showOverlayTime: false,
            showOverlayConnectionStatus: true,
            overlayOpacity: 0.7,
            OverlayPosition.TopLeft,
            existingPlayer: shared);
    }

    private static IVideoPlayer Player(PlayerState state)
    {
        var player = Substitute.For<IVideoPlayer>();
        player.State.Returns(state);
        return player;
    }
}