namespace AtcSoft.Video.Surveillance.Wpf.Core.UserControls;

using ConnectionState = Atc.Network.ConnectionState;
using UiCameraConfiguration = AtcSoft.Video.Surveillance.Wpf.Core.Models.CameraConfiguration;

/// <summary>
/// Moving a camera to another tile (a swap, a layout change) moves its player, its recording and its
/// connection state with it, whether it is connected or not. Before, a swap with a camera that was not
/// connected reconnected both tiles, and each tile's recording stayed behind with the other camera.
/// </summary>
public sealed class CameraTileSwapTests
{
    [Fact]
    public void A_Camera_Keeps_Its_Player_Recording_And_State_When_Swapped_With_A_Camera_That_Is_Not_Connected()
    {
        var error = WpfTestThread.Run(() =>
        {
            var door = new UiCameraConfiguration();
            door.Display.DisplayName = "Door";
            var yard = new UiCameraConfiguration();
            yard.Display.DisplayName = "Yard";
            var doorPlayer = Substitute.For<IVideoPlayer>();
            var yardPlayer = Substitute.For<IVideoPlayer>();

            var first = new CameraTile { Camera = door, Player = doorPlayer, ConnectionState = ConnectionState.Connected, RecordingState = RecordingState.Recording };
            var second = new CameraTile { Camera = yard, Player = yardPlayer, ConnectionState = ConnectionState.ConnectionFailed };

            first.PrepareForSwap();
            second.PrepareForSwap();
            (first.Camera, second.Camera) = (yard, door);
            first.SwapPlayerWith(second);
            first.CompleteSwap();
            second.CompleteSwap();

            second.Camera.Should().BeSameAs(door);
            second.Player.Should().BeSameAs(doorPlayer);
            second.ConnectionState.Should().Be(ConnectionState.Connected);
            second.RecordingState.Should().Be(RecordingState.Recording);

            first.Camera.Should().BeSameAs(yard);
            first.Player.Should().BeSameAs(yardPlayer);
            first.ConnectionState.Should().Be(ConnectionState.ConnectionFailed);
            first.RecordingState.Should().Be(RecordingState.Idle);
        });

        error.Should().BeNull();
    }

    [Fact]
    public void A_Camera_Whose_Address_Cannot_Be_Read_Fails_To_Connect_Instead_Of_Ending_The_App()
    {
        // A camera without a host: the tile's reconnect timer built its address on the UI thread,
        // and the exception ended the app (a test host on a build server, here).
        var error = WpfTestThread.Run(() =>
        {
            var camera = new UiCameraConfiguration();
            camera.Display.DisplayName = "Nowhere";
            var tile = new CameraTile { Camera = camera, Player = Substitute.For<IVideoPlayer>() };

            tile.Reconnect();
            tile.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            tile.ConnectionState.Should().Be(ConnectionState.ConnectionFailed);
        });

        error.Should().BeNull();
    }
}