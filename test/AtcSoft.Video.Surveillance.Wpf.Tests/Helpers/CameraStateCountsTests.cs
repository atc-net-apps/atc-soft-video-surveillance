namespace AtcSoft.Video.Surveillance.Wpf.Helpers;

/// <summary>
/// The connected and recording counts come from each camera's own state, so
/// repeated events cannot make them drift.
/// </summary>
public sealed class CameraStateCountsTests
{
    [Fact]
    public void Repeated_And_Unrelated_Events_Do_Not_Make_The_Counts_Drift()
    {
        var front = TestCameras.Network() with { ConnectionState = CameraConnectionState.Connected, IsRecording = true };
        var back = TestCameras.Network() with { ConnectionState = CameraConnectionState.Disconnected };
        var sut = new CameraStateCounts();
        sut.Load([front, back]);

        // Connecting, reconnecting and disconnected again: one camera is still not connected.
        sut.Apply(Connection(back.Id, CameraConnectionState.Connecting));
        sut.Apply(Connection(back.Id, CameraConnectionState.Reconnecting));
        sut.Apply(Connection(back.Id, CameraConnectionState.Disconnected));
        sut.Apply(Connection(front.Id, CameraConnectionState.Connected));
        sut.Apply(Connection(front.Id, CameraConnectionState.Connected));

        sut.Connected.Should().Be(1);
        sut.Recording.Should().Be(1);
    }

    [Fact]
    public void A_Load_Sets_Every_Count_Again()
    {
        var front = TestCameras.Network() with { ConnectionState = CameraConnectionState.Connected };
        var sut = new CameraStateCounts();
        sut.Load([front]);
        sut.Apply(Connection(Guid.NewGuid(), CameraConnectionState.Connected));

        sut.Load([front with { ConnectionState = CameraConnectionState.Disconnected }]);

        sut.Connected.Should().Be(0);
    }

    private static ConnectionStateChangedEvent Connection(
        Guid cameraId,
        CameraConnectionState state)
        => new(CameraId: cameraId, NewState: state, Timestamp: DateTimeOffset.UtcNow);
}