namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

public sealed class CameraStatusBoardTests
{
    private static readonly Guid Front = Guid.NewGuid();
    private static readonly Guid Garden = Guid.NewGuid();
    private static readonly Guid Gate = Guid.NewGuid();

    [Fact]
    public void Counts_Come_From_The_Cameras_As_Loaded()
    {
        var sut = new CameraStatusBoard();

        sut.Load(
        [
            (Front, IsConnected: true, IsRecording: true),
            (Garden, IsConnected: true, IsRecording: false),
            (Gate, IsConnected: false, IsRecording: false),
        ]);

        sut.CameraCount.Should().Be(3);
        sut.ConnectedCount.Should().Be(2);
        sut.RecordingCount.Should().Be(1);
    }

    [Fact]
    public void A_Camera_Is_Counted_Once_However_Often_It_Reports_Connected()
    {
        var sut = new CameraStatusBoard();
        sut.Load([(Front, IsConnected: false, IsRecording: false)]);

        sut.ApplyConnectionState(Front, new CameraConnectionState("Connected"));
        sut.ApplyConnectionState(Front, new CameraConnectionState("Connected"));

        sut.ConnectedCount.Should().Be(1);
    }

    [Theory]
    [InlineData("Disconnected")]
    [InlineData("Error")]
    [InlineData("Connecting")]
    [InlineData("Reconnecting")]
    public void Any_State_But_Connected_Is_Not_Connected(string state)
    {
        var sut = new CameraStatusBoard();
        sut.Load([(Front, IsConnected: true, IsRecording: false)]);

        sut.ApplyConnectionState(Front, new CameraConnectionState(state));

        sut.ConnectedCount.Should().Be(0);
    }

    [Fact]
    public void A_New_Segment_Does_Not_Change_The_Recording_Count()
    {
        // A segment switch reports Recording to Idle for the old file and
        // Idle to Recording for the new one.
        var sut = new CameraStatusBoard();
        sut.Load([(Front, IsConnected: true, IsRecording: true)]);

        sut.ApplyRecordingState(Front, new RecordingStatusState("Idle"));
        sut.ApplyRecordingState(Front, new RecordingStatusState("Recording"));

        sut.RecordingCount.Should().Be(1);
    }

    [Theory]
    [InlineData("Recording", true)]
    [InlineData("RecordingMotion", true)]
    [InlineData("RecordingPostMotion", true)]
    [InlineData("recording", true)]
    [InlineData("Idle", false)]
    [InlineData("", false)]
    [InlineData("paused", false)]
    public void Every_Recording_State_Counts_As_Recording(
        string state,
        bool expected)
    {
        var sut = new CameraStatusBoard();
        sut.Load([(Front, IsConnected: true, IsRecording: !expected)]);

        sut.ApplyRecordingState(Front, new RecordingStatusState(state));

        sut.IsRecording(Front).Should().Be(expected);
    }

    [Fact]
    public void An_Event_For_A_Camera_That_Was_Added_Meanwhile_Is_Counted()
    {
        var sut = new CameraStatusBoard();
        sut.Load([(Front, IsConnected: false, IsRecording: false)]);

        sut.ApplyConnectionState(Garden, new CameraConnectionState("Connected"));

        sut.CameraCount.Should().Be(2);
        sut.ConnectedCount.Should().Be(1);
    }

    [Fact]
    public void An_Event_From_The_Stream_Updates_Its_Camera()
    {
        var sut = new CameraStatusBoard();
        sut.Load([(Front, IsConnected: false, IsRecording: false)]);
        var now = DateTimeOffset.UtcNow;

        sut.Apply(new SurveillanceEvent(
            Type: "connectionStateChanged",
            CameraId: Front,
            Timestamp: now,
            Connection: new ConnectionStateChangedEvent(Front, CameraConnectionState.Connected, now)));
        sut.Apply(new SurveillanceEvent(
            Type: "recordingStateChanged",
            CameraId: Front,
            Timestamp: now,
            Recording: new RecordingStateChangedEvent(Front, RecordingStatusState.RecordingMotion, RecordingStatusState.Idle, Timestamp: now)));

        sut.IsConnected(Front).Should().BeTrue();
        sut.IsRecording(Front).Should().BeTrue();
    }

    [Fact]
    public void An_Event_That_Does_Not_Change_A_State_Leaves_The_Camera_As_It_Is()
    {
        var sut = new CameraStatusBoard();
        sut.Load([(Front, IsConnected: true, IsRecording: true)]);

        sut.Apply(new SurveillanceEvent(Type: "motionDetected", CameraId: Front, Timestamp: DateTimeOffset.UtcNow));
        sut.Apply(new SurveillanceEvent(Type: "somethingNew", CameraId: Front, Timestamp: DateTimeOffset.UtcNow));

        sut.IsConnected(Front).Should().BeTrue();
        sut.IsRecording(Front).Should().BeTrue();
    }
}