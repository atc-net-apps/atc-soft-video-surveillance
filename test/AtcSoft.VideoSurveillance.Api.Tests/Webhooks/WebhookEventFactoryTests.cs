namespace AtcSoft.VideoSurveillance.Api.Webhooks;

public sealed class WebhookEventFactoryTests
{
    private static readonly Guid CameraId = Guid.NewGuid();
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "recordings");

    [Theory]
    [InlineData(RecordingState.Idle, RecordingState.Recording, "recording.started")]
    [InlineData(RecordingState.Idle, RecordingState.RecordingMotion, "recording.started")]
    [InlineData(RecordingState.Recording, RecordingState.Idle, "recording.stopped")]
    [InlineData(RecordingState.RecordingPostMotion, RecordingState.Idle, "recording.stopped")]
    public void A_Recording_That_Starts_Or_Stops_Is_An_Event(
        RecordingState oldState,
        RecordingState newState,
        string expected)
    {
        var file = Path.Combine(Root, "Front Door", "2026-10-13", "Front Door_20261013_101500_000.mp4");

        var result = WebhookEventFactory.FromRecording(
            new AtcSoft.VideoSurveillance.Events.RecordingStateChangedEventArgs(CameraId, oldState, newState, file),
            "Front Door",
            Root);

        result!.Type.Should().Be(expected);
        result.CameraId.Should().Be(CameraId);
        result.CameraName.Should().Be("Front Door");
        result.Data.FilePath.Should().Be("Front Door/2026-10-13/Front Door_20261013_101500_000.mp4", "the path is the one the API lists the recording under");
    }

    [Theory]
    [InlineData(RecordingState.Recording, RecordingState.RecordingMotion)]
    [InlineData(RecordingState.RecordingMotion, RecordingState.RecordingPostMotion)]
    [InlineData(RecordingState.Idle, RecordingState.Idle)]
    public void A_Change_Between_Kinds_Of_Recording_Is_Not_An_Event(
        RecordingState oldState,
        RecordingState newState)
        => WebhookEventFactory.FromRecording(
                new AtcSoft.VideoSurveillance.Events.RecordingStateChangedEventArgs(CameraId, oldState, newState, filePath: null),
                "Front Door",
                Root)
            .Should().BeNull();

    [Theory]
    [InlineData(ConnectionState.Connected, "camera.connected")]
    [InlineData(ConnectionState.Disconnected, "camera.disconnected")]
    [InlineData(ConnectionState.Error, "camera.disconnected")]
    public void A_Camera_That_Connects_Or_Is_Lost_Is_An_Event(
        ConnectionState state,
        string expected)
    {
        var result = WebhookEventFactory.FromConnection(CameraId, "Front Door", state);

        result!.Type.Should().Be(expected);
        result.Data.State.Should().Be(state.ToString());
    }

    [Theory]
    [InlineData(ConnectionState.Connecting)]
    [InlineData(ConnectionState.Reconnecting)]
    public void A_Camera_That_Is_Still_Trying_Is_Not_An_Event(
        ConnectionState state)
        => WebhookEventFactory.FromConnection(CameraId, "Front Door", state).Should().BeNull();

    [Theory]
    [InlineData(true, "motion.started")]
    [InlineData(false, "motion.stopped")]
    public void Motion_That_Starts_Or_Stops_Is_An_Event(
        bool isMotionActive,
        string expected)
    {
        var result = WebhookEventFactory.FromMotion(
            new AtcSoft.VideoSurveillance.Events.MotionDetectedEventArgs(CameraId, 4.5, isMotionActive),
            "Front Door");

        result.Type.Should().Be(expected);
        result.Data.ChangePercentage.Should().Be(4.5);
    }
}