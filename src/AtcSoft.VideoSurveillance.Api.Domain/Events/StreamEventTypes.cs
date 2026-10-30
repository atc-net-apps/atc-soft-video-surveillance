namespace AtcSoft.VideoSurveillance.Api.Domain.Events;

/// <summary>
/// The types of the events of GET /events: the hub's names for them.
/// </summary>
public static class StreamEventTypes
{
    public const string ConnectionStateChanged = "connectionStateChanged";
    public const string RecordingStateChanged = "recordingStateChanged";
    public const string MotionDetected = "motionDetected";
    public const string UsbCameraLifecycleChanged = "usbCameraLifecycleChanged";
    public const string ActivityChanged = "activityChanged";
}