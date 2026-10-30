namespace AtcSoft.VideoSurveillance.Wpf;

/// <summary>
/// A camera as the server sends it.
/// </summary>
internal static class TestCameras
{
    public static global::VideoSurveillance.Generated.Cameras.Models.Camera Network(
        global::VideoSurveillance.Generated.Cameras.Models.CameraRotation? rotation = null)
        => new(
            Id: Guid.NewGuid(),
            DisplayName: "Gate",
            Description: string.Empty,
            Source: global::VideoSurveillance.Generated.Cameras.Models.CameraSource.Network,
            ConnectionType: global::VideoSurveillance.Generated.Cameras.Models.CameraConnectionType.None,
            Rotation: rotation ?? global::VideoSurveillance.Generated.Cameras.Models.CameraRotation.None,
            IpAddress: "192.0.2.10",
            Port: 554,
            Protocol: global::VideoSurveillance.Generated.Cameras.Models.CameraProtocol.Rtsp,
            Path: "/stream1",
            Username: string.Empty,
            OverlayPosition: global::VideoSurveillance.Generated.Cameras.Models.CameraOverlayPosition.TopLeft,
            StreamUseLowLatencyMode: false,
            StreamMaxLatencyMs: 0,
            StreamRtspTransport: global::VideoSurveillance.Generated.Cameras.Models.CameraStreamRtspTransport.Tcp,
            StreamBufferDurationMs: 0,
            IsRecording: false,
            MotionZones: []);
}