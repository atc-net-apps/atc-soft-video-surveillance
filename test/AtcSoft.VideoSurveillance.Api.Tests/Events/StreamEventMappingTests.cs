namespace AtcSoft.VideoSurveillance.Api.Events;

/// <summary>
/// Every hub event is also one event of GET /events, under the hub's name
/// for it, so the stream and the hub say the same.
/// </summary>
public sealed class StreamEventMappingTests
{
    private static readonly Guid CameraId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_Connection_Change_Is_A_ConnectionStateChanged_Event()
    {
        var hubEvent = new ConnectionStateChangedEvent(CameraId, CameraConnectionState.Connected, Now);

        var streamEvent = hubEvent.ToStreamEvent();

        streamEvent.Should().Be(new SurveillanceEvent(Type: "connectionStateChanged", CameraId: CameraId, Timestamp: Now, Connection: hubEvent));
    }

    [Fact]
    public void A_Recording_Change_Is_A_RecordingStateChanged_Event()
    {
        var hubEvent = new RecordingStateChangedEvent(CameraId, RecordingStatusState.Recording, RecordingStatusState.Idle, Timestamp: Now);

        var streamEvent = hubEvent.ToStreamEvent();

        streamEvent.Should().Be(new SurveillanceEvent(Type: "recordingStateChanged", CameraId: CameraId, Timestamp: Now, Recording: hubEvent));
    }

    [Fact]
    public void A_Motion_Is_A_MotionDetected_Event()
    {
        var hubEvent = new MotionDetectedEvent(CameraId, IsMotionActive: true, ChangePercentage: 3, BoundingBoxes: [], AnalysisWidth: 320, AnalysisHeight: 240, Timestamp: Now);

        var streamEvent = hubEvent.ToStreamEvent();

        streamEvent.Should().Be(new SurveillanceEvent(Type: "motionDetected", CameraId: CameraId, Timestamp: Now, Motion: hubEvent));
    }

    [Fact]
    public void A_Usb_Change_Is_A_UsbCameraLifecycleChanged_Event()
    {
        var hubEvent = new UsbCameraLifecycleEvent(CameraId, global::VideoSurveillance.Generated.Events.Models.UsbCameraLifecyclePhase.Unplugged, "device", "Logitech BRIO", Now);

        var streamEvent = hubEvent.ToStreamEvent();

        streamEvent.Should().Be(new SurveillanceEvent(Type: "usbCameraLifecycleChanged", CameraId: CameraId, Timestamp: Now, Usb: hubEvent));
    }
}