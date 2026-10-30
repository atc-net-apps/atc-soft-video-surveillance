namespace AtcSoft.VideoSurveillance.Api.Mapping;

/// <summary>
/// The hub's events carry the specification's models and values, the same
/// vocabulary as the REST API (<c>connected</c>, <c>recordingMotion</c>).
/// </summary>
public sealed class HubEventMappingTests
{
    private static readonly Guid CameraId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 8, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(AtcSoft.VideoSurveillance.Enums.ConnectionState.Connected, "connected")]
    [InlineData(AtcSoft.VideoSurveillance.Enums.ConnectionState.Error, "error")]
    [InlineData(AtcSoft.VideoSurveillance.Enums.ConnectionState.DeviceUnplugged, "deviceUnplugged")]
    [InlineData(AtcSoft.VideoSurveillance.Enums.ConnectionState.Disconnected, "disconnected")]
    public void A_Connection_Change_Carries_The_State_As_The_Api_Writes_It(
        AtcSoft.VideoSurveillance.Enums.ConnectionState state,
        string expected)
    {
        var hubEvent = HubEventMapping.ToHubEvent(CameraId, state, Now);

        hubEvent.CameraId.Should().Be(CameraId);
        hubEvent.NewState.ToWireValue().Should().Be(expected);
        hubEvent.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void A_Recording_Change_Carries_Both_States_And_The_File()
    {
        var e = new RecordingStateChangedEventArgs(
            CameraId,
            AtcSoft.VideoSurveillance.Enums.RecordingState.Recording,
            AtcSoft.VideoSurveillance.Enums.RecordingState.RecordingMotion,
            @"Gate\2026-10-15\Gate_20261015_083000.mp4");

        var hubEvent = e.ToHubEvent();

        hubEvent.CameraId.Should().Be(CameraId);
        hubEvent.OldState.Should().Be(RecordingStatusState.Recording);
        hubEvent.NewState.Should().Be(RecordingStatusState.RecordingMotion);
        hubEvent.FilePath.Should().Be(@"Gate\2026-10-15\Gate_20261015_083000.mp4");
    }

    [Fact]
    public void A_Motion_Carries_Its_Boxes_In_The_Analysed_Picture()
    {
        var e = new MotionDetectedEventArgs(
            CameraId,
            changePercentage: 4.5,
            isMotionActive: true,
            boundingBoxes: [new BoundingBox { X = 10, Y = 20, Width = 30, Height = 40, TrackId = 7 }],
            analysisWidth: 320,
            analysisHeight: 240);

        var hubEvent = e.ToHubEvent();

        hubEvent.IsMotionActive.Should().BeTrue();
        hubEvent.ChangePercentage.Should().Be(4.5);
        hubEvent.AnalysisWidth.Should().Be(320);
        hubEvent.AnalysisHeight.Should().Be(240);
        hubEvent.BoundingBoxes.Should().ContainSingle()
            .Which.Should().Be(new MotionBox(X: 10, Y: 20, Width: 30, Height: 40, TrackId: 7), "a client follows each box by its track");
    }

    [Theory]
    [InlineData(AtcSoft.VideoSurveillance.Enums.UsbCameraLifecyclePhase.Unplugged, global::VideoSurveillance.Generated.Events.Models.UsbCameraLifecyclePhase.Unplugged)]
    [InlineData(AtcSoft.VideoSurveillance.Enums.UsbCameraLifecyclePhase.Replugged, global::VideoSurveillance.Generated.Events.Models.UsbCameraLifecyclePhase.Replugged)]
    public void A_Usb_Camera_Change_Carries_Its_Phase_And_Device(
        AtcSoft.VideoSurveillance.Enums.UsbCameraLifecyclePhase phase,
        global::VideoSurveillance.Generated.Events.Models.UsbCameraLifecyclePhase expected)
    {
        var device = new AtcSoft.VideoSurveillance.Models.UsbDeviceDescriptor(@"\\?\usb#vid_046d", friendlyName: "Logitech BRIO");
        var e = new UsbCameraLifecycleChangedEventArgs(CameraId, phase, device);

        var hubEvent = e.ToHubEvent(Now);

        hubEvent.Phase.Should().Be(expected);
        hubEvent.DeviceId.Should().Be(@"\\?\usb#vid_046d");
        hubEvent.FriendlyName.Should().Be("Logitech BRIO");
        hubEvent.Timestamp.Should().Be(Now);
    }

    [Fact]
    public void An_Activity_Change_Names_The_Recording_As_The_Listing_Does_And_Goes_Out_As_Its_Own_Type()
    {
        var root = Path.Combine(Path.GetTempPath(), "recordings");
        var cameraId = Guid.NewGuid();
        var change = new AtcSoft.VideoSurveillance.Events.CameraActivityChangedEventArgs(
            cameraId,
            isActive: false,
            start: Now,
            end: Now.AddSeconds(7),
            videoPath: Path.Combine(root, "Garden", "Garden_20261019_090000_000.mkv"),
            offsetSeconds: 42,
            peakLevel: 18,
            tracks: 2);

        var streamEvent = change.ToEvent(root).ToStreamEvent();

        streamEvent.Type.Should().Be("activityChanged");
        streamEvent.CameraId.Should().Be(cameraId);
        streamEvent.Timestamp.Should().Be(Now.AddSeconds(7), "an ended activity is told when it ended");
        streamEvent.Activity.Should().Be(new ActivityChangedEvent(cameraId, false, Now, "Garden/Garden_20261019_090000_000.mkv", 42, 18, 2, Now.AddSeconds(7)));
    }
}